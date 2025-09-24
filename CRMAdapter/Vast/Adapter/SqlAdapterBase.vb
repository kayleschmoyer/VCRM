'-----------------------------------------------------------------------
' File: SqlAdapterBase.vb
' Purpose: Provides shared SQL helper functionality for VAST Desktop adapters.
' Security Considerations: Applies retry and rate limiting policies, sanitizes command construction, and wraps exceptions to prevent leaking sensitive backend data.
' Example Usage: `Await ExecuteDbOperationAsync("CustomerAdapter.GetById", Function(ct) command.ExecuteReaderAsync(ct), token)`
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.Common
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports CRMAdapter.CommonConfig
Imports CRMAdapter.CommonContracts
Imports CRMAdapter.CommonInfrastructure

Namespace CRMAdapter.Vast.Adapter
    ''' <summary>
    ''' Base class that unifies SQL helper behavior across VB.NET adapters.
    ''' </summary>
    Public MustInherit Class SqlAdapterBase
        Implements IDisposable

        Private ReadOnly _connectionLock As New SemaphoreSlim(1, 1)
        Private _disposed As Boolean

        ''' <summary>
        ''' Initializes a new instance of the <see cref="SqlAdapterBase"/> class.
        ''' </summary>
        ''' <param name="connection">Database connection.</param>
        ''' <param name="fieldMap">Field map configuration.</param>
        ''' <param name="retryPolicy">Retry policy implementation.</param>
        ''' <param name="logger">Structured logger.</param>
        ''' <param name="rateLimiter">Rate limiter implementation.</param>
        Protected Sub New(connection As DbConnection, fieldMap As FieldMap, Optional retryPolicy As ISqlRetryPolicy = Nothing, Optional logger As IAdapterLogger = Nothing, Optional rateLimiter As IAdapterRateLimiter = Nothing)
            If connection Is Nothing Then
                Throw New ArgumentNullException(NameOf(connection))
            End If

            If fieldMap Is Nothing Then
                Throw New ArgumentNullException(NameOf(fieldMap))
            End If

            Me.Connection = connection
            Me.FieldMap = fieldMap
            Me.Logger = If(logger, NullAdapterLogger.Instance)
            Me.RateLimiter = If(rateLimiter, NoopAdapterRateLimiter.Instance)
            Me.RetryPolicy = If(retryPolicy, New ExponentialBackoffRetryPolicy(logger:=Me.Logger))
        End Sub

        ''' <summary>
        ''' Gets the database connection.
        ''' </summary>
        Protected ReadOnly Property Connection As DbConnection

        ''' <summary>
        ''' Gets the field map configuration.
        ''' </summary>
        Protected ReadOnly Property FieldMap As FieldMap

        ''' <summary>
        ''' Gets the retry policy for SQL operations.
        ''' </summary>
        Protected ReadOnly Property RetryPolicy As ISqlRetryPolicy

        ''' <summary>
        ''' Gets the adapter logger.
        ''' </summary>
        Protected ReadOnly Property Logger As IAdapterLogger

        ''' <summary>
        ''' Gets the rate limiter used for throttling.
        ''' </summary>
        Protected ReadOnly Property RateLimiter As IAdapterRateLimiter

        ''' <summary>
        ''' Gets the backend name.
        ''' </summary>
        Protected ReadOnly Property BackendName As String
            Get
                Return FieldMap.BackendName
            End Get
        End Property

        ''' <summary>
        ''' Creates a command ensuring the connection is open.
        ''' </summary>
        Protected Async Function CreateCommandAsync(commandText As String, cancellationToken As CancellationToken) As Task(Of DbCommand)
            If String.IsNullOrWhiteSpace(commandText) Then
                Throw New ArgumentException("Command text must be provided.", NameOf(commandText))
            End If

            Await EnsureConnectionAsync(cancellationToken).ConfigureAwait(False)
            Dim command = Connection.CreateCommand()
            command.CommandText = commandText
            command.CommandType = CommandType.Text
            Return command
        End Function

        ''' <summary>
        ''' Adds a parameter to the provided command.
        ''' </summary>
        Protected Shared Sub AddParameter(command As DbCommand, name As String, value As Object, Optional dbType As DbType? = Nothing)
            If command Is Nothing Then
                Throw New ArgumentNullException(NameOf(command))
            End If

            If String.IsNullOrWhiteSpace(name) Then
                Throw New ArgumentException("Parameter name must be provided.", NameOf(name))
            End If

            Dim parameter = command.CreateParameter()
            parameter.ParameterName = name
            parameter.Value = If(value, DBNull.Value)
            If dbType.HasValue Then
                parameter.DbType = dbType.Value
            End If

            command.Parameters.Add(parameter)
        End Sub

        ''' <summary>
        ''' Executes a database operation with retry, throttling, and safe exception wrapping.
        ''' </summary>
        Protected Async Function ExecuteDbOperationAsync(Of TResult)(operationName As String, operation As Func(Of CancellationToken, Task(Of TResult)), cancellationToken As CancellationToken) As Task(Of TResult)
            If String.IsNullOrWhiteSpace(operationName) Then
                Throw New ArgumentException("Operation name must be provided.", NameOf(operationName))
            End If

            If operation Is Nothing Then
                Throw New ArgumentNullException(NameOf(operation))
            End If

            Using scope = AdapterCorrelationScope.BeginScope()
                Dim baseContext As New Dictionary(Of String, Object) From
                {
                    {"Operation", operationName},
                    {"Backend", BackendName},
                    {"CorrelationId", scope.CorrelationId}
                }

                Dim stopwatch = Stopwatch.StartNew()
                Logger.LogInformation("CRM query started.", baseContext)

                Try
                    Dim result = Await RetryPolicy.ExecuteAsync(Function(ct) ExecuteCoreAsync(operationName, operation, ct, baseContext, stopwatch), cancellationToken).ConfigureAwait(False)

                    Dim completionContext = New Dictionary(Of String, Object)(baseContext)
                    completionContext("DurationMs") = stopwatch.Elapsed.TotalMilliseconds
                    Logger.LogInformation("CRM query completed.", completionContext)
                    Return result
                Catch ex As AdapterDataAccessException
                    Throw
                Finally
                    If stopwatch.IsRunning Then
                        stopwatch.Stop()
                    End If
                End Try
            End Using
        End Function

        ''' <summary>
        ''' Executes an operation that does not return a result.
        ''' </summary>
        Protected Async Function ExecuteDbOperationAsync(operationName As String, operation As Func(Of CancellationToken, Task), cancellationToken As CancellationToken) As Task
            If operation Is Nothing Then
                Throw New ArgumentNullException(NameOf(operation))
            End If

            Await ExecuteDbOperationAsync(Of Object)(operationName, Async Function(ct)
                                                                                Await operation(ct).ConfigureAwait(False)
                                                                                Return Nothing
                                                                            End Function, cancellationToken).ConfigureAwait(False)
        End Function

        Private Async Function ExecuteCoreAsync(Of TResult)(operationName As String, operation As Func(Of CancellationToken, Task(Of TResult)), cancellationToken As CancellationToken, baseContext As IReadOnlyDictionary(Of String, Object), stopwatch As Stopwatch) As Task(Of TResult)
            Dim lease = Await RateLimiter.AcquireAsync(operationName, cancellationToken).ConfigureAwait(False)
            Using lease
                Try
                    Return Await operation(cancellationToken).ConfigureAwait(False)
                Catch ex As AdapterException
                    Throw
                Catch ex As MappingConfigurationException
                    Throw
                Catch ex As OperationCanceledException
                    Throw
                Catch ex As Exception
                    Dim failureContext = New Dictionary(Of String, Object)(baseContext)
                    failureContext("DurationMs") = stopwatch.Elapsed.TotalMilliseconds
                    failureContext("ExceptionType") = ex.GetType().FullName
                    Logger.LogError("CRM query failed.", ex, failureContext)
                    Throw New AdapterDataAccessException(operationName, BackendName, ex)
                End Try
            End Using
        End Function

        ''' <summary>
        ''' Applies safe bounds to requested limits.
        ''' </summary>
        Protected Shared Function EnforceLimit(requested As Integer, defaultLimit As Integer, parameterName As String) As Integer
            If String.IsNullOrWhiteSpace(parameterName) Then
                Throw New ArgumentException("Parameter name must be provided.", NameOf(parameterName))
            End If

            If defaultLimit <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(defaultLimit))
            End If

            If requested <= 0 Then
                Return defaultLimit
            End If

            If requested > defaultLimit Then
                Throw New InvalidAdapterRequestException(String.Format("{0} cannot exceed {1}.", parameterName, defaultLimit))
            End If

            Return requested
        End Function

        ''' <summary>
        ''' Disposes the adapter and underlying connection.
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        ''' <summary>
        ''' Performs the dispose pattern logic.
        ''' </summary>
        ''' <param name="disposing">Indicates whether managed resources should be disposed.</param>
        Protected Overridable Sub Dispose(disposing As Boolean)
            If _disposed Then
                Return
            End If

            If disposing Then
                Connection.Dispose()
                _connectionLock.Dispose()
            End If

            _disposed = True
        End Sub

        Private Async Function EnsureConnectionAsync(cancellationToken As CancellationToken) As Task
            If Connection.State = ConnectionState.Open Then
                Return
            End If

            Await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                If Connection.State <> ConnectionState.Open Then
                    Await Connection.OpenAsync(cancellationToken).ConfigureAwait(False)
                End If
            Finally
                _connectionLock.Release()
            End Try
        End Function
    End Class
End Namespace

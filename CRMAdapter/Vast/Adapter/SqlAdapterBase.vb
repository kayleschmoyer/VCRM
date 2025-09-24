'-----------------------------------------------------------------------
' File: SqlAdapterBase.vb
' Role: Provides shared SQL helper functionality for VAST Desktop adapters.
' Architectural Purpose: Centralizes async connection management and parameter handling for legacy SQL Server.
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Data
Imports System.Data.Common
Imports System.Threading
Imports System.Threading.Tasks
Imports CRMAdapter.CommonConfig

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
        Protected Sub New(connection As DbConnection, fieldMap As FieldMap)
            If connection Is Nothing Then
                Throw New ArgumentNullException(NameOf(connection))
            End If

            If fieldMap Is Nothing Then
                Throw New ArgumentNullException(NameOf(fieldMap))
            End If

            Me.Connection = connection
            Me.FieldMap = fieldMap
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
        ''' Creates a command ensuring the connection is open.
        ''' </summary>
        Protected Async Function CreateCommandAsync(commandText As String, cancellationToken As CancellationToken) As Task(Of DbCommand)
            If commandText Is Nothing Then
                Throw New ArgumentNullException(NameOf(commandText))
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

            If name Is Nothing Then
                Throw New ArgumentNullException(NameOf(name))
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

'-----------------------------------------------------------------------
' File: AppointmentAdapter.vb
' Purpose: Implements canonical appointment access for the VAST Desktop backend.
' Security Considerations: Validates identifiers and date ranges, clamps list limits, parameterizes SQL, and runs through retry/rate-limited execution to avoid exposing backend faults.
' Example Usage: `Dim appointments = Await adapter.GetByDateAsync(Date.Today, 100, CancellationToken.None)`
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Data
Imports System.Data.Common
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports CRMAdapter.CommonConfig
Imports CRMAdapter.CommonContracts
Imports CRMAdapter.CommonDomain
Imports CRMAdapter.CommonInfrastructure

Namespace CRMAdapter.Vast.Adapter
    ''' <summary>
    ''' Legacy VAST Desktop implementation of the <see cref="IAppointmentAdapter"/> contract.
    ''' </summary>
    Public NotInheritable Class AppointmentAdapter
        Inherits SqlAdapterBase
        Implements IAppointmentAdapter

        Private Const DefaultListLimit As Integer = 100

        Private Shared ReadOnly RequiredAppointmentKeys As String() = {
            "Appointment.__source",
            "Appointment.Id",
            "Appointment.CustomerId",
            "Appointment.VehicleId",
            "Appointment.Start",
            "Appointment.End",
            "Appointment.Advisor",
            "Appointment.Status",
            "Appointment.Location"
        }

        Private Shared ReadOnly AppointmentProjectionFields As String() = {
            "Id",
            "CustomerId",
            "VehicleId",
            "Start",
            "End",
            "Advisor",
            "Status",
            "Location"
        }

        Private ReadOnly _defaultListLimit As Integer

        ''' <summary>
        ''' Initializes a new instance of the <see cref="AppointmentAdapter"/> class.
        ''' </summary>
        Public Sub New(
            connection As DbConnection,
            fieldMap As FieldMap,
            Optional retryPolicy As ISqlRetryPolicy = Nothing,
            Optional logger As IAdapterLogger = Nothing,
            Optional rateLimiter As IAdapterRateLimiter = Nothing,
            Optional defaultListLimit As Integer = DefaultListLimit)
            MyBase.New(connection, fieldMap, retryPolicy, logger, rateLimiter)
            If defaultListLimit <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(defaultListLimit), "Default list limit must be positive.")
            End If

            _defaultListLimit = defaultListLimit
            MappingValidator.EnsureMappings(fieldMap, RequiredAppointmentKeys, NameOf(AppointmentAdapter))
            MappingValidator.EnsureEntitySources(fieldMap, New String() {"Appointment"}, NameOf(AppointmentAdapter))
        End Sub

        ''' <inheritdoc />
        Public Function GetByIdAsync(id As Guid, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Appointment) Implements IAppointmentAdapter.GetByIdAsync
            If id = Guid.Empty Then
                Throw New InvalidAdapterRequestException("Appointment id must be provided.")
            End If

            Return ExecuteDbOperationAsync(Of Appointment)(
                "Vast.AppointmentAdapter.GetById",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Appointment")
                    Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
                    Dim commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap("Id")} = @id"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        AddParameter(command, "@id", id)
                        Dim reader = Await command.ExecuteReaderAsync(ct).ConfigureAwait(False)
                        Using reader
                            If Not Await reader.ReadAsync(ct).ConfigureAwait(False) Then
                                Return Nothing
                            End If
                            Return ReadAppointment(reader)
                        End Using
                    End Using
                End Function,
                cancellationToken)
        End Function

        ''' <inheritdoc />
        Public Function GetByDateAsync([date] As DateTime, maxResults As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyCollection(Of Appointment)) Implements IAppointmentAdapter.GetByDateAsync
            Dim limit = EnforceLimit(maxResults, _defaultListLimit, NameOf(maxResults))
            Dim startOfDay = [date].Date
            Dim endOfDay = startOfDay.AddDays(1)

            Return ExecuteDbOperationAsync(Of IReadOnlyCollection(Of Appointment))(
                "Vast.AppointmentAdapter.GetByDate",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Appointment", AppointmentProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Appointment")
                    Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
                    Dim startField = fieldMap("Start")
                    Dim commandText = $"SELECT TOP (@limit) {selectClause} FROM {source} WHERE {startField} >= @start AND {startField} < @end ORDER BY {startField}"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        AddParameter(command, "@limit", limit, DbType.Int32)
                        AddParameter(command, "@start", startOfDay, DbType.DateTime2)
                        AddParameter(command, "@end", endOfDay, DbType.DateTime2)

                        Dim appointments As New List(Of Appointment)()
                        Dim reader = Await command.ExecuteReaderAsync(ct).ConfigureAwait(False)
                        Using reader
                            While Await reader.ReadAsync(ct).ConfigureAwait(False)
                                appointments.Add(ReadAppointment(reader))
                            End While
                        End Using

                        Return CType(New ReadOnlyCollection(Of Appointment)(appointments), IReadOnlyCollection(Of Appointment))
                    End Using
                End Function,
                cancellationToken)
        End Function

        Private Shared Function ReadAppointment(reader As DbDataReader) As Appointment
            Return New Appointment(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")),
                reader.GetGuid(reader.GetOrdinal("VehicleId")),
                reader.GetDateTime(reader.GetOrdinal("Start")),
                reader.GetDateTime(reader.GetOrdinal("End")),
                reader.GetString(reader.GetOrdinal("Advisor")),
                reader.GetString(reader.GetOrdinal("Status")),
                reader.GetString(reader.GetOrdinal("Location")))
        End Function
    End Class
End Namespace

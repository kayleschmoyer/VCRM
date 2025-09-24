'-----------------------------------------------------------------------
' File: VehicleAdapter.vb
' Purpose: Implements canonical vehicle access for the VAST Desktop backend.
' Security Considerations: Validates identifiers, bounds list limits, parameterizes SQL, and routes database operations through retry and throttling policies.
' Example Usage: `Dim vehicles = Await adapter.GetByCustomerAsync(customerId, 25, CancellationToken.None)`
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
    ''' Legacy VAST Desktop implementation of the <see cref="IVehicleAdapter"/> contract.
    ''' </summary>
    Public NotInheritable Class VehicleAdapter
        Inherits SqlAdapterBase
        Implements IVehicleAdapter

        Private Const DefaultListLimit As Integer = 100

        Private Shared ReadOnly RequiredVehicleKeys As String() = {
            "Vehicle.__source",
            "Vehicle.Id",
            "Vehicle.CustomerId",
            "Vehicle.Vin",
            "Vehicle.Make",
            "Vehicle.Model",
            "Vehicle.ModelYear",
            "Vehicle.Odometer"
        }

        Private Shared ReadOnly VehicleProjectionFields As String() = {
            "Id",
            "CustomerId",
            "Vin",
            "Make",
            "Model",
            "ModelYear",
            "Odometer"
        }

        Private ReadOnly _defaultListLimit As Integer

        ''' <summary>
        ''' Initializes a new instance of the <see cref="VehicleAdapter"/> class.
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
            MappingValidator.EnsureMappings(fieldMap, RequiredVehicleKeys, NameOf(VehicleAdapter))
            MappingValidator.EnsureEntitySources(fieldMap, New String() {"Vehicle"}, NameOf(VehicleAdapter))
        End Sub

        ''' <inheritdoc />
        Public Function GetByIdAsync(id As Guid, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Vehicle) Implements IVehicleAdapter.GetByIdAsync
            If id = Guid.Empty Then
                Throw New InvalidAdapterRequestException("Vehicle id must be provided.")
            End If

            Return ExecuteDbOperationAsync(Of Vehicle)(
                "Vast.VehicleAdapter.GetById",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Vehicle")
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
                            Return ReadVehicle(reader)
                        End Using
                    End Using
                End Function,
                cancellationToken)
        End Function

        ''' <inheritdoc />
        Public Function GetByCustomerAsync(customerId As Guid, maxResults As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyCollection(Of Vehicle)) Implements IVehicleAdapter.GetByCustomerAsync
            If customerId = Guid.Empty Then
                Throw New InvalidAdapterRequestException("Customer id must be provided.")
            End If

            Dim limit = EnforceLimit(maxResults, _defaultListLimit, NameOf(maxResults))

            Return ExecuteDbOperationAsync(Of IReadOnlyCollection(Of Vehicle))(
                "Vast.VehicleAdapter.GetByCustomer",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Vehicle", VehicleProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Vehicle")
                    Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
                    Dim commandText = $"SELECT TOP (@limit) {selectClause} FROM {source} WHERE {fieldMap("CustomerId")} = @customerId ORDER BY {fieldMap("ModelYear")} DESC, {fieldMap("Make")}, {fieldMap("Model")}"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        AddParameter(command, "@limit", limit, DbType.Int32)
                        AddParameter(command, "@customerId", customerId)

                        Dim vehicles As New List(Of Vehicle)()
                        Dim reader = Await command.ExecuteReaderAsync(ct).ConfigureAwait(False)
                        Using reader
                            While Await reader.ReadAsync(ct).ConfigureAwait(False)
                                vehicles.Add(ReadVehicle(reader))
                            End While
                        End Using

                        Return CType(New ReadOnlyCollection(Of Vehicle)(vehicles), IReadOnlyCollection(Of Vehicle))
                    End Using
                End Function,
                cancellationToken)
        End Function

        Private Shared Function ReadVehicle(reader As DbDataReader) As Vehicle
            Return New Vehicle(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Vin")),
                reader.GetString(reader.GetOrdinal("Make")),
                reader.GetString(reader.GetOrdinal("Model")),
                reader.GetInt32(reader.GetOrdinal("ModelYear")),
                reader.GetInt32(reader.GetOrdinal("Odometer")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")))
        End Function
    End Class
End Namespace

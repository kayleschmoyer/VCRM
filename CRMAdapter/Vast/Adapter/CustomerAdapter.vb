'-----------------------------------------------------------------------
' File: CustomerAdapter.vb
' Purpose: Implements the canonical customer adapter for the VAST Desktop backend.
' Security Considerations: Validates all input, enforces rate limiting, parameterizes SQL, and wraps database exceptions to prevent sensitive leakage to COM callers.
' Example Usage: `Dim customer = Await adapter.GetByIdAsync(customerId, CancellationToken.None)`
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
    ''' Legacy VAST Desktop implementation of the <see cref="ICustomerAdapter"/> contract.
    ''' </summary>
    Public NotInheritable Class CustomerAdapter
        Inherits SqlAdapterBase
        Implements ICustomerAdapter

        Private Const DefaultSearchLimit As Integer = 50
        Private Const DefaultListLimit As Integer = 100
        Private Const MaxNameQueryLength As Integer = 128

        Private Shared ReadOnly RequiredCustomerKeys As String() = {
            "Customer.__source",
            "Customer.Id",
            "Customer.Name",
            "Customer.Email",
            "Customer.Phone",
            "Customer.AddressLine1",
            "Customer.AddressLine2",
            "Customer.City",
            "Customer.State",
            "Customer.PostalCode",
            "Customer.Country",
            "Customer.ModifiedOn"
        }

        Private Shared ReadOnly RequiredVehicleReferenceKeys As String() = {
            "Vehicle.__source",
            "Vehicle.Id",
            "Vehicle.Vin",
            "Vehicle.CustomerId"
        }

        Private Shared ReadOnly CustomerProjectionFields As String() = {
            "Id",
            "Name",
            "Email",
            "Phone",
            "AddressLine1",
            "AddressLine2",
            "City",
            "State",
            "PostalCode",
            "Country"
        }

        Private ReadOnly _defaultSearchLimit As Integer
        Private ReadOnly _defaultListLimit As Integer

        ''' <summary>
        ''' Initializes a new instance of the <see cref="CustomerAdapter"/> class.
        ''' </summary>
        Public Sub New(
            connection As DbConnection,
            fieldMap As FieldMap,
            Optional retryPolicy As ISqlRetryPolicy = Nothing,
            Optional logger As IAdapterLogger = Nothing,
            Optional rateLimiter As IAdapterRateLimiter = Nothing,
            Optional defaultSearchLimit As Integer = DefaultSearchLimit,
            Optional defaultListLimit As Integer = DefaultListLimit)
            MyBase.New(connection, fieldMap, retryPolicy, logger, rateLimiter)
            If defaultSearchLimit <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(defaultSearchLimit), "Default search limit must be positive.")
            End If

            If defaultListLimit <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(defaultListLimit), "Default list limit must be positive.")
            End If

            _defaultSearchLimit = defaultSearchLimit
            _defaultListLimit = defaultListLimit
            MappingValidator.EnsureMappings(fieldMap, RequiredCustomerKeys, NameOf(CustomerAdapter))
            MappingValidator.EnsureMappings(fieldMap, RequiredVehicleReferenceKeys, NameOf(CustomerAdapter))
            MappingValidator.EnsureEntitySources(fieldMap, New String() {"Customer", "Vehicle"}, NameOf(CustomerAdapter))
        End Sub

        ''' <inheritdoc />
        Public Function GetByIdAsync(id As Guid, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Customer) Implements ICustomerAdapter.GetByIdAsync
            If id = Guid.Empty Then
                Throw New InvalidAdapterRequestException("Customer id must be provided.")
            End If

            Return ExecuteDbOperationAsync(Of Customer)(
                "Vast.CustomerAdapter.GetById",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Customer")
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

                            Dim snapshot = ReadCustomerSnapshot(reader)
                            Dim vehicleLookup = Await LoadVehicleReferencesAsync(New Guid() {snapshot.Id}, ct).ConfigureAwait(False)
                            Dim vehicles As IReadOnlyCollection(Of VehicleReference) = Nothing
                            If Not vehicleLookup.TryGetValue(snapshot.Id, vehicles) Then
                                vehicles = Array.Empty(Of VehicleReference)()
                            End If
                            Return snapshot.ToCustomer(vehicles)
                        End Using
                    End Using
                End Function,
                cancellationToken)
        End Function

        ''' <inheritdoc />
        Public Function SearchByNameAsync(nameQuery As String, maxResults As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyCollection(Of Customer)) Implements ICustomerAdapter.SearchByNameAsync
            Dim sanitizedQuery = SanitizeNameQuery(nameQuery)
            Dim limit = EnforceLimit(maxResults, _defaultSearchLimit, NameOf(maxResults))

            Return ExecuteDbOperationAsync(Of IReadOnlyCollection(Of Customer))(
                "Vast.CustomerAdapter.SearchByName",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Customer")
                    Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
                    Dim commandText = $"SELECT TOP (@limit) {selectClause} FROM {source} WHERE {fieldMap("Name")} LIKE @name ORDER BY {fieldMap("Name")}"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        AddParameter(command, "@limit", limit, DbType.Int32)
                        AddParameter(command, "@name", $"%{sanitizedQuery}%")

                        Dim snapshots = Await ReadSnapshotsAsync(command, ct).ConfigureAwait(False)
                        Dim vehicleLookup = Await LoadVehicleReferencesAsync(snapshots.Select(Function(s) s.Id), ct).ConfigureAwait(False)

                        Dim customers = snapshots.Select(Function(snapshot)
                                                             Dim vehicles As IReadOnlyCollection(Of VehicleReference) = Nothing
                                                             If Not vehicleLookup.TryGetValue(snapshot.Id, vehicles) Then
                                                                 vehicles = Array.Empty(Of VehicleReference)()
                                                             End If
                                                             Return snapshot.ToCustomer(vehicles)
                                                         End Function).ToList()
                        Return CType(New ReadOnlyCollection(Of Customer)(customers), IReadOnlyCollection(Of Customer))
                    End Using
                End Function,
                cancellationToken)
        End Function

        ''' <inheritdoc />
        Public Function GetRecentCustomersAsync(maxResults As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyCollection(Of Customer)) Implements ICustomerAdapter.GetRecentCustomersAsync
            Dim limit = EnforceLimit(maxResults, _defaultListLimit, NameOf(maxResults))

            Return ExecuteDbOperationAsync(Of IReadOnlyCollection(Of Customer))(
                "Vast.CustomerAdapter.GetRecent",
                Async Function(ct)
                    Dim fieldMap = FieldMap.GetTargets("Customer", CustomerProjectionFields)
                    Dim source = FieldMap.GetEntitySource("Customer")
                    Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
                    Dim modifiedOnField = FieldMap.GetTarget("Customer.ModifiedOn")
                    Dim commandText = $"SELECT TOP (@limit) {selectClause} FROM {source} ORDER BY {modifiedOnField} DESC"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        AddParameter(command, "@limit", limit, DbType.Int32)
                        Dim snapshots = Await ReadSnapshotsAsync(command, ct).ConfigureAwait(False)
                        Dim vehicleLookup = Await LoadVehicleReferencesAsync(snapshots.Select(Function(s) s.Id), ct).ConfigureAwait(False)

                        Dim customers = snapshots.Select(Function(snapshot)
                                                             Dim vehicles As IReadOnlyCollection(Of VehicleReference) = Nothing
                                                             If Not vehicleLookup.TryGetValue(snapshot.Id, vehicles) Then
                                                                 vehicles = Array.Empty(Of VehicleReference)()
                                                             End If
                                                             Return snapshot.ToCustomer(vehicles)
                                                         End Function).ToList()
                        Return CType(New ReadOnlyCollection(Of Customer)(customers), IReadOnlyCollection(Of Customer))
                    End Using
                End Function,
                cancellationToken)
        End Function

        Private Function LoadVehicleReferencesAsync(customerIds As IEnumerable(Of Guid), cancellationToken As CancellationToken) As Task(Of IReadOnlyDictionary(Of Guid, IReadOnlyCollection(Of VehicleReference)))
            If customerIds Is Nothing Then
                Throw New ArgumentNullException(NameOf(customerIds))
            End If

            Return ExecuteDbOperationAsync(Of IReadOnlyDictionary(Of Guid, IReadOnlyCollection(Of VehicleReference)))(
                "Vast.CustomerAdapter.LoadVehicleReferences",
                Async Function(ct)
                    Dim idList = customerIds.Distinct().Where(Function(id) id <> Guid.Empty).ToList()
                    If idList.Count = 0 Then
                        Return CType(New Dictionary(Of Guid, IReadOnlyCollection(Of VehicleReference))(), IReadOnlyDictionary(Of Guid, IReadOnlyCollection(Of VehicleReference)))
                    End If

                    Dim fields = FieldMap.GetTargets("Vehicle", New String() {"Id", "Vin", "CustomerId"})
                    Dim source = FieldMap.GetEntitySource("Vehicle")
                    Dim parameterNames = idList.Select(Function(_, index) $"@c{index}").ToArray()
                    Dim selectClause = $"{fields("Id")} AS [VehicleId], {fields("Vin")} AS [Vin], {fields("CustomerId")} AS [CustomerId]"
                    Dim commandText = $"SELECT {selectClause} FROM {source} WHERE {fields("CustomerId")} IN ({String.Join(", ", parameterNames)})"

                    Dim command = Await CreateCommandAsync(commandText, ct).ConfigureAwait(False)
                    Using command
                        For i = 0 To idList.Count - 1
                            AddParameter(command, parameterNames(i), idList(i))
                        Next

                        Dim accumulator As New Dictionary(Of Guid, List(Of VehicleReference))()
                        Dim reader = Await command.ExecuteReaderAsync(ct).ConfigureAwait(False)
                        Using reader
                            While Await reader.ReadAsync(ct).ConfigureAwait(False)
                                Dim customerId = reader.GetGuid(reader.GetOrdinal("CustomerId"))
                                Dim vehicleId = reader.GetGuid(reader.GetOrdinal("VehicleId"))
                                Dim vin = reader.GetString(reader.GetOrdinal("Vin"))

                                Dim vehicles As List(Of VehicleReference) = Nothing
                                If Not accumulator.TryGetValue(customerId, vehicles) Then
                                    vehicles = New List(Of VehicleReference)()
                                    accumulator(customerId) = vehicles
                                End If

                                vehicles.Add(New VehicleReference(vehicleId, vin))
                            End While
                        End Using

                        Return CType(accumulator.ToDictionary(Function(pair) pair.Key, Function(pair) CType(pair.Value.AsReadOnly(), IReadOnlyCollection(Of VehicleReference))), IReadOnlyDictionary(Of Guid, IReadOnlyCollection(Of VehicleReference)))
                    End Using
                End Function,
                cancellationToken)
        End Function

        Private Shared Async Function ReadSnapshotsAsync(command As DbCommand, cancellationToken As CancellationToken) As Task(Of List(Of CustomerSnapshot))
            Dim snapshots As New List(Of CustomerSnapshot)()
            Dim reader = Await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
            Using reader
                While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                    snapshots.Add(ReadCustomerSnapshot(reader))
                End While
            End Using

            Return snapshots
        End Function

        Private Shared Function ReadCustomerSnapshot(reader As DbDataReader) As CustomerSnapshot
            Return New CustomerSnapshot(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("Phone")),
                reader.GetString(reader.GetOrdinal("AddressLine1")),
                If(reader.IsDBNull(reader.GetOrdinal("AddressLine2")), String.Empty, reader.GetString(reader.GetOrdinal("AddressLine2"))),
                reader.GetString(reader.GetOrdinal("City")),
                reader.GetString(reader.GetOrdinal("State")),
                reader.GetString(reader.GetOrdinal("PostalCode")),
                reader.GetString(reader.GetOrdinal("Country")))
        End Function

        Private Shared Function SanitizeNameQuery(nameQuery As String) As String
            If String.IsNullOrWhiteSpace(nameQuery) Then
                Throw New InvalidAdapterRequestException("Name query must be supplied.")
            End If

            Dim trimmed = nameQuery.Trim()
            If trimmed.Length > MaxNameQueryLength Then
                Throw New InvalidAdapterRequestException($"Name query cannot exceed {MaxNameQueryLength} characters.")
            End If

            Return trimmed
        End Function

        Private NotInheritable Class CustomerSnapshot
            Public Sub New(id As Guid, displayName As String, email As String, primaryPhone As String, line1 As String, line2 As String, city As String, state As String, postalCode As String, country As String)
                Me.Id = id
                Me.DisplayName = displayName
                Me.Email = email
                Me.PrimaryPhone = primaryPhone
                Me.Line1 = line1
                Me.Line2 = line2
                Me.City = city
                Me.State = state
                Me.PostalCode = postalCode
                Me.Country = country
            End Sub

            Public ReadOnly Property Id As Guid
            Public ReadOnly Property DisplayName As String
            Public ReadOnly Property Email As String
            Public ReadOnly Property PrimaryPhone As String
            Public ReadOnly Property Line1 As String
            Public ReadOnly Property Line2 As String
            Public ReadOnly Property City As String
            Public ReadOnly Property State As String
            Public ReadOnly Property PostalCode As String
            Public ReadOnly Property Country As String

            Public Function ToCustomer(vehicles As IReadOnlyCollection(Of VehicleReference)) As Customer
                Dim address = New PostalAddress(Line1, Line2, City, State, PostalCode, Country)
                Return New Customer(Id, DisplayName, Email, PrimaryPhone, address, vehicles)
            End Function
        End Class
    End Class
End Namespace

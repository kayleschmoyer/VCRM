'-----------------------------------------------------------------------
' File: InvoiceAdapter.vb
' Role: Implements canonical invoice access for the VAST Desktop backend.
' Architectural Purpose: Projects legacy billing data into the CRM canonical invoice aggregate including line items.
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

Namespace CRMAdapter.Vast.Adapter
    ''' <summary>
    ''' Legacy VAST Desktop implementation of the <see cref="IInvoiceAdapter"/> contract.
    ''' </summary>
    Public NotInheritable Class InvoiceAdapter
        Inherits SqlAdapterBase
        Implements IInvoiceAdapter

        Private Const DefaultListLimit As Integer = 100

        Private Shared ReadOnly RequiredInvoiceKeys As String() = {
            "Invoice.__source",
            "Invoice.Id",
            "Invoice.CustomerId",
            "Invoice.VehicleId",
            "Invoice.Number",
            "Invoice.Date",
            "Invoice.Total",
            "Invoice.Status"
        }

        Private Shared ReadOnly RequiredInvoiceLineKeys As String() = {
            "InvoiceLine.__source",
            "InvoiceLine.InvoiceId",
            "InvoiceLine.Description",
            "InvoiceLine.Quantity",
            "InvoiceLine.UnitPrice",
            "InvoiceLine.Tax"
        }

        Private Shared ReadOnly InvoiceProjectionFields As String() = {
            "Id",
            "CustomerId",
            "VehicleId",
            "Number",
            "Date",
            "Total",
            "Status"
        }

        Private ReadOnly _defaultListLimit As Integer

        ''' <summary>
        ''' Initializes a new instance of the <see cref="InvoiceAdapter"/> class.
        ''' </summary>
        Public Sub New(connection As DbConnection, fieldMap As FieldMap, Optional defaultListLimit As Integer = DefaultListLimit)
            MyBase.New(connection, fieldMap)
            _defaultListLimit = defaultListLimit
            MappingValidator.EnsureMappings(fieldMap, RequiredInvoiceKeys, NameOf(InvoiceAdapter))
            MappingValidator.EnsureMappings(fieldMap, RequiredInvoiceLineKeys, NameOf(InvoiceAdapter))
        End Sub

        ''' <inheritdoc />
        Public Async Function GetByIdAsync(id As Guid, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Invoice) Implements IInvoiceAdapter.GetByIdAsync
            Dim fieldMap = Me.FieldMap.GetTargets("Invoice", InvoiceProjectionFields)
            Dim source = Me.FieldMap.GetEntitySource("Invoice")
            Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
            Dim commandText = $"SELECT {selectClause} FROM {source} WHERE {fieldMap("Id")} = @id"

            Dim command = Await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(False)
            Using command
                AddParameter(command, "@id", id)
                Dim reader = Await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                Using reader
                    If Not Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                        Return Nothing
                    End If

                    Dim invoiceRecord = ReadInvoice(reader)
                    Dim lines = Await LoadInvoiceLinesAsync(New Guid() {invoiceRecord.Id}, cancellationToken).ConfigureAwait(False)
                    Dim lineItems As IReadOnlyCollection(Of InvoiceLine) = Nothing
                    If Not lines.TryGetValue(invoiceRecord.Id, lineItems) Then
                        lineItems = Array.Empty(Of InvoiceLine)()
                    End If

                    Return New Invoice(invoiceRecord.Id, invoiceRecord.CustomerId, invoiceRecord.VehicleId, invoiceRecord.InvoiceNumber, invoiceRecord.InvoiceDate, invoiceRecord.TotalAmount, invoiceRecord.Status, lineItems)
                End Using
            End Using
        End Function

        ''' <inheritdoc />
        Public Async Function GetByCustomerAsync(customerId As Guid, maxResults As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyCollection(Of Invoice)) Implements IInvoiceAdapter.GetByCustomerAsync
            Dim limit = Math.Min(_defaultListLimit, If(maxResults > 0, maxResults, _defaultListLimit))
            Dim fieldMap = Me.FieldMap.GetTargets("Invoice", InvoiceProjectionFields)
            Dim source = Me.FieldMap.GetEntitySource("Invoice")
            Dim selectClause = String.Join(", ", fieldMap.Select(Function(kvp) $"{kvp.Value} AS [{kvp.Key}]"))
            Dim commandText = $"SELECT TOP (@limit) {selectClause} FROM {source} WHERE {fieldMap("CustomerId")} = @customerId ORDER BY {fieldMap("Date")} DESC"

            Dim command = Await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(False)
            Using command
                AddParameter(command, "@limit", limit, DbType.Int32)
                AddParameter(command, "@customerId", customerId)
                Dim invoices As New List(Of InvoiceRecord)()
                Dim reader = Await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                Using reader
                    While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                        invoices.Add(ReadInvoice(reader))
                    End While
                End Using

                Dim lineLookup = Await LoadInvoiceLinesAsync(invoices.Select(Function(i) i.Id), cancellationToken).ConfigureAwait(False)
                Dim results = invoices.Select(Function(record)
                                                  Dim lineItems As IReadOnlyCollection(Of InvoiceLine) = Nothing
                                                  If Not lineLookup.TryGetValue(record.Id, lineItems) Then
                                                      lineItems = Array.Empty(Of InvoiceLine)()
                                                  End If
                                                  Return New Invoice(record.Id, record.CustomerId, record.VehicleId, record.InvoiceNumber, record.InvoiceDate, record.TotalAmount, record.Status, lineItems)
                                              End Function).ToList()
                Return New ReadOnlyCollection(Of Invoice)(results)
            End Using
        End Function

        Private Async Function LoadInvoiceLinesAsync(invoiceIds As IEnumerable(Of Guid), cancellationToken As CancellationToken) As Task(Of IReadOnlyDictionary(Of Guid, IReadOnlyCollection(Of InvoiceLine)))
            Dim ids = invoiceIds.Distinct().ToList()
            If ids.Count = 0 Then
                Return New Dictionary(Of Guid, IReadOnlyCollection(Of InvoiceLine))()
            End If

            Dim fields = Me.FieldMap.GetTargets("InvoiceLine", New String() {"InvoiceId", "Description", "Quantity", "UnitPrice", "Tax"})
            Dim source = Me.FieldMap.GetEntitySource("InvoiceLine")
            Dim parameterNames = ids.Select(Function(_, index) $"@i{index}").ToArray()
            Dim selectClause = $"{fields("InvoiceId")} AS [InvoiceId], {fields("Description")} AS [Description], {fields("Quantity")} AS [Quantity], {fields("UnitPrice")} AS [UnitPrice], {fields("Tax")} AS [Tax]"
            Dim commandText = $"SELECT {selectClause} FROM {source} WHERE {fields("InvoiceId")} IN ({String.Join(", ", parameterNames)})"

            Dim command = Await CreateCommandAsync(commandText, cancellationToken).ConfigureAwait(False)
            Using command
                For i = 0 To ids.Count - 1
                    AddParameter(command, parameterNames(i), ids(i))
                Next

                Dim accumulator As New Dictionary(Of Guid, List(Of InvoiceLine))()
                Dim reader = Await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                Using reader
                    While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                        Dim invoiceId = reader.GetGuid(reader.GetOrdinal("InvoiceId"))
                        Dim description = reader.GetString(reader.GetOrdinal("Description"))
                        Dim quantity = reader.GetDecimal(reader.GetOrdinal("Quantity"))
                        Dim unitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice"))
                        Dim tax = reader.GetDecimal(reader.GetOrdinal("Tax"))

                        Dim lines As List(Of InvoiceLine) = Nothing
                        If Not accumulator.TryGetValue(invoiceId, lines) Then
                            lines = New List(Of InvoiceLine)()
                            accumulator(invoiceId) = lines
                        End If

                        lines.Add(New InvoiceLine(description, quantity, unitPrice, tax))
                    End While
                End Using

                Return accumulator.ToDictionary(Function(pair) pair.Key, Function(pair) CType(pair.Value.AsReadOnly(), IReadOnlyCollection(Of InvoiceLine)))
            End Using
        End Function

        Private Shared Function ReadInvoice(reader As DbDataReader) As InvoiceRecord
            Return New InvoiceRecord(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("CustomerId")),
                reader.GetGuid(reader.GetOrdinal("VehicleId")),
                reader.GetString(reader.GetOrdinal("Number")),
                reader.GetDateTime(reader.GetOrdinal("Date")),
                reader.GetDecimal(reader.GetOrdinal("Total")),
                reader.GetString(reader.GetOrdinal("Status")))
        End Function

        Private NotInheritable Class InvoiceRecord
            Public Sub New(id As Guid, customerId As Guid, vehicleId As Guid, invoiceNumber As String, invoiceDate As DateTime, totalAmount As Decimal, status As String)
                Me.Id = id
                Me.CustomerId = customerId
                Me.VehicleId = vehicleId
                Me.InvoiceNumber = invoiceNumber
                Me.InvoiceDate = invoiceDate
                Me.TotalAmount = totalAmount
                Me.Status = status
            End Sub

            Public ReadOnly Property Id As Guid
            Public ReadOnly Property CustomerId As Guid
            Public ReadOnly Property VehicleId As Guid
            Public ReadOnly Property InvoiceNumber As String
            Public ReadOnly Property InvoiceDate As DateTime
            Public ReadOnly Property TotalAmount As Decimal
            Public ReadOnly Property Status As String
        End Class
    End Class
End Namespace

'-----------------------------------------------------------------------
' File: ComAdapterWrapper.vb
' Role: Exposes COM-friendly methods for VB6 clients to consume canonical CRM data via JSON payloads.
' Architectural Purpose: Bridges legacy VB6 UIs with the modern adapter framework without schema awareness.
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Runtime.InteropServices
Imports System.Text.Json
Imports CRMAdapter.CommonContracts
Imports CRMAdapter.CommonDomain

Namespace CRMAdapter.Vast.Interop
    ''' <summary>
    ''' COM visible wrapper exposing JSON-based access to canonical CRM objects.
    ''' </summary>
    <ComVisible(True)>
    <Guid("C6CB1E24-7092-4DC4-9EC6-74D7C53AC1DE")>
    <ClassInterface(ClassInterfaceType.None)>
    Public NotInheritable Class ComAdapterWrapper
        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
            .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            .WriteIndented = False
        }

        Private ReadOnly _customerAdapter As ICustomerAdapter
        Private ReadOnly _vehicleAdapter As IVehicleAdapter
        Private ReadOnly _invoiceAdapter As IInvoiceAdapter
        Private ReadOnly _appointmentAdapter As IAppointmentAdapter

        ''' <summary>
        ''' Initializes a new instance of the <see cref="ComAdapterWrapper"/> class.
        ''' </summary>
        Public Sub New(customerAdapter As ICustomerAdapter, vehicleAdapter As IVehicleAdapter, invoiceAdapter As IInvoiceAdapter, appointmentAdapter As IAppointmentAdapter)
            _customerAdapter = customerAdapter
            _vehicleAdapter = vehicleAdapter
            _invoiceAdapter = invoiceAdapter
            _appointmentAdapter = appointmentAdapter
        End Sub

        ''' <summary>
        ''' Retrieves a customer and vehicles serialized as JSON for VB6 consumption.
        ''' </summary>
        Public Function GetCustomerByIdJson(id As String) As String
            Try
                Dim customerId = Guid.Parse(id)
                Dim customer = _customerAdapter.GetByIdAsync(customerId).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(customer)
            Catch ex As Exception
                Throw New COMException("CRM-CUSTOMER-ERROR", &H8004A100, ex)
            End Try
        End Function

        ''' <summary>
        ''' Retrieves vehicles for a customer serialized to JSON.
        ''' </summary>
        Public Function GetVehiclesByCustomerJson(customerId As String) As String
            Try
                Dim id = Guid.Parse(customerId)
                Dim vehicles = _vehicleAdapter.GetByCustomerAsync(id, 100).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(vehicles)
            Catch ex As Exception
                Throw New COMException("CRM-VEHICLE-ERROR", &H8004A101, ex)
            End Try
        End Function

        ''' <summary>
        ''' Retrieves invoices for a customer serialized to JSON.
        ''' </summary>
        Public Function GetInvoicesByCustomerJson(customerId As String) As String
            Try
                Dim id = Guid.Parse(customerId)
                Dim invoices = _invoiceAdapter.GetByCustomerAsync(id, 100).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(invoices)
            Catch ex As Exception
                Throw New COMException("CRM-INVOICE-ERROR", &H8004A102, ex)
            End Try
        End Function

        ''' <summary>
        ''' Retrieves appointments for a specific date serialized to JSON.
        ''' </summary>
        Public Function GetAppointmentsByDateJson([date] As Date) As String
            Try
                Dim appointments = _appointmentAdapter.GetByDateAsync([date], 100).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(appointments)
            Catch ex As Exception
                Throw New COMException("CRM-APPOINTMENT-ERROR", &H8004A103, ex)
            End Try
        End Function

        Private Shared Function Serialize(Of T)(value As T) As String
            If value Is Nothing Then
                Return String.Empty
            End If

            Return JsonSerializer.Serialize(value, JsonOptions)
        End Function
    End Class
End Namespace

'-----------------------------------------------------------------------
' File: ComAdapterWrapper.vb
' Purpose: Exposes COM-friendly methods for VB6 clients to consume canonical CRM data via JSON payloads.
' Security Considerations: Validates all input, sanitizes JSON serialization with safe encoders, and converts internal exceptions into non-leaking COM fault codes.
' Example Usage: `Dim json = wrapper.GetCustomerByIdJson("9F6A...")`
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Runtime.InteropServices
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports CRMAdapter.CommonContracts
Imports CRMAdapter.CommonDomain
Imports CRMAdapter.CommonInfrastructure

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
            .WriteIndented = False,
            .Encoder = JavaScriptEncoder.Default,
            .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }

        Private ReadOnly _customerAdapter As ICustomerAdapter
        Private ReadOnly _vehicleAdapter As IVehicleAdapter
        Private ReadOnly _invoiceAdapter As IInvoiceAdapter
        Private ReadOnly _appointmentAdapter As IAppointmentAdapter
        Private ReadOnly _logger As IAdapterLogger

        ''' <summary>
        ''' Initializes a new instance of the <see cref="ComAdapterWrapper"/> class.
        ''' </summary>
        Public Sub New(
            customerAdapter As ICustomerAdapter,
            vehicleAdapter As IVehicleAdapter,
            invoiceAdapter As IInvoiceAdapter,
            appointmentAdapter As IAppointmentAdapter,
            Optional logger As IAdapterLogger = Nothing)
            If customerAdapter Is Nothing Then
                Throw New ArgumentNullException(NameOf(customerAdapter))
            End If

            If vehicleAdapter Is Nothing Then
                Throw New ArgumentNullException(NameOf(vehicleAdapter))
            End If

            If invoiceAdapter Is Nothing Then
                Throw New ArgumentNullException(NameOf(invoiceAdapter))
            End If

            If appointmentAdapter Is Nothing Then
                Throw New ArgumentNullException(NameOf(appointmentAdapter))
            End If

            _customerAdapter = customerAdapter
            _vehicleAdapter = vehicleAdapter
            _invoiceAdapter = invoiceAdapter
            _appointmentAdapter = appointmentAdapter
            _logger = If(logger, NullAdapterLogger.Instance)
        End Sub

        ''' <summary>
        ''' Retrieves a customer and vehicles serialized as JSON for VB6 consumption.
        ''' </summary>
        Public Function GetCustomerByIdJson(id As String) As String
            Try
                Dim customerId = ParseGuid(id, NameOf(id))
                Dim customer = _customerAdapter.GetByIdAsync(customerId).ConfigureAwait(False).GetAwaiter().GetResult()
                If customer Is Nothing Then
                    Throw New CustomerNotFoundException(customerId)
                End If
                Return Serialize(customer)
            Catch ex As Exception
                Throw CreateComException("CRM-CUSTOMER-ERROR", &H8004A100, ex)
            End Try
        End Function

        ''' <summary>
        ''' Retrieves vehicles for a customer serialized to JSON.
        ''' </summary>
        Public Function GetVehiclesByCustomerJson(customerId As String) As String
            Try
                Dim idValue = ParseGuid(customerId, NameOf(customerId))
                Dim vehicles = _vehicleAdapter.GetByCustomerAsync(idValue, 100).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(vehicles)
            Catch ex As Exception
                Throw CreateComException("CRM-VEHICLE-ERROR", &H8004A101, ex)
            End Try
        End Function

        ''' <summary>
        ''' Retrieves invoices for a customer serialized to JSON.
        ''' </summary>
        Public Function GetInvoicesByCustomerJson(customerId As String) As String
            Try
                Dim idValue = ParseGuid(customerId, NameOf(customerId))
                Dim invoices = _invoiceAdapter.GetByCustomerAsync(idValue, 100).ConfigureAwait(False).GetAwaiter().GetResult()
                Return Serialize(invoices)
            Catch ex As Exception
                Throw CreateComException("CRM-INVOICE-ERROR", &H8004A102, ex)
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
                Throw CreateComException("CRM-APPOINTMENT-ERROR", &H8004A103, ex)
            End Try
        End Function

        Private Function CreateComException(messagePrefix As String, errorCode As Integer, ex As Exception) As COMException
            Dim sanitizedMessage As String = messagePrefix
            Dim adapterException = TryCast(ex, AdapterException)
            If adapterException IsNot Nothing Then
                sanitizedMessage = $"{messagePrefix}: {adapterException.ErrorCode}"
            End If

            _logger.LogError(
                "COM adapter operation failed.",
                ex,
                New Dictionary(Of String, Object) From {
                    {"MessagePrefix", messagePrefix},
                    {"ExceptionType", ex.GetType().FullName}
                })

            Return New COMException(sanitizedMessage, errorCode, ex)
        End Function

        Private Shared Function ParseGuid(value As String, parameterName As String) As Guid
            If String.IsNullOrWhiteSpace(value) Then
                Throw New InvalidAdapterRequestException($"{parameterName} must be supplied.")
            End If

            Dim trimmed = value.Trim()
            Dim guidValue As Guid
            If Not Guid.TryParse(trimmed, guidValue) Then
                Throw New InvalidAdapterRequestException($"{parameterName} is not a valid GUID.")
            End If

            Return guidValue
        End Function

        Private Shared Function Serialize(Of T)(value As T) As String
            If value Is Nothing Then
                Return String.Empty
            End If

            Return JsonSerializer.Serialize(value, JsonOptions)
        End Function
    End Class
End Namespace

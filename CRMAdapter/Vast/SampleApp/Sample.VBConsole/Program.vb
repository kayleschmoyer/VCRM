'-----------------------------------------------------------------------
' File: Program.vb
' Role: Demonstrates consuming the VAST Desktop adapters from a VB.NET console application.
' Architectural Purpose: Provides a quickstart for legacy teams to access canonical CRM data via the adapter factory.
'-----------------------------------------------------------------------
Option Strict On
Option Explicit On

Imports System
Imports System.Data.SqlClient
Imports System.Text.Json
Imports CRMAdapter.Factory

Module Program
    ''' <summary>
    ''' Sample entry point showing how to bootstrap the adapter factory for VAST Desktop.
    ''' </summary>
    Sub Main()
        Dim connectionString = Environment.GetEnvironmentVariable("CRM_DESKTOP_CONNECTION")
        If String.IsNullOrWhiteSpace(connectionString) Then
            Console.WriteLine("Set CRM_DESKTOP_CONNECTION to point to the VAST Desktop SQL Server instance.")
            Return
        End If

        Dim mappingPath = "CRMAdapter/Vast/Mapping/vast-desktop.json"
        Dim adapters = AdapterFactory.CreateVastDesktop(Function() New SqlConnection(connectionString), mappingPath)

        Dim sampleCustomerId = Guid.NewGuid()
        Dim customer = adapters.CustomerAdapter.GetByIdAsync(sampleCustomerId).GetAwaiter().GetResult()
        If customer Is Nothing Then
            Console.WriteLine($"Customer {sampleCustomerId} not found.")
            Return
        End If

        Dim json = JsonSerializer.Serialize(customer, New JsonSerializerOptions With {.WriteIndented = True})
        Console.WriteLine("Canonical Customer Payload:")
        Console.WriteLine(json)
    End Sub
End Module

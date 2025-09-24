# Real-time SignalR Integration

The CRM Adapter now provides live CRM updates using SignalR between the API and the Blazor UI. This document outlines how to run the hub, configure clients, and reason about the connection lifecycle.

## Running the API hub

1. Navigate to the API project and launch it as normal:
   ```bash
   cd CRMAdapter/CRMAdapter.Api
   dotnet run
   ```
2. The SignalR hub is hosted at `/crmhub` on the API. Ensure HTTPS is enabled (default for the project).
3. The hub requires JWT authentication with the same roles enforced across the rest of the API. The accepted roles are `Admin`, `Manager`, `Clerk`, and `Tech`.

## Dispatching events from the API

* `CRMAdapter.Api/Hubs/CrmEventsHub.cs` exposes the strongly typed `ICrmEventsClient` interface.
* Adapters publish events through `CRMAdapter.Api/Events/EventDispatcher.cs`. After resolving the hub context, it broadcasts the event and includes the current correlation identifier.
* To broadcast a new event from server logic, call one of the dispatcher methods, e.g. `await EventDispatcher.BroadcastCustomerCreatedAsync(payload);`.

## Blazor UI integration

* `CRMAdapter.UI/Services/Realtime/RealtimeHubConnection.cs` encapsulates the SignalR `HubConnection`.
  * It automatically reconnects with exponential backoff, emits snackbars on reconnection, and opens a circuit breaker after repeated failures.
  * The hub URL is read from configuration (`Realtime:HubUrl`). In development it defaults to `https://localhost:5001/crmhub`.
* Specialized scoped services (`CustomerRealtimeService`, `InvoiceRealtimeService`, `VehicleRealtimeService`, and `AppointmentRealtimeService`) expose typed C# callbacks and user-facing toasts.
* Pages subscribe to these services for instant UI updates:
  * Customers list performs slide-in and highlight animations for new/updated rows.
  * Invoices list updates status, balance, and triggers success snackbars for payments.
  * Dashboard refreshes KPI data whenever any event fires.

## Configuration

* Update `CRMAdapter.UI/appsettings.Development.json` if the API hub URL changes.
* The UI uses the current JWT session when connecting to the hub. Make sure users authenticate before establishing the realtime connection.

## Testing the flow

1. Start both the API and UI projects.
2. Sign in to the UI to acquire a JWT token.
3. Perform an action that emits an event (e.g., create a customer via the API). The Customers page should update immediately with a slide-in animation.
4. Trigger an invoice payment; the Invoices page should mark the row as paid and display a toast.
5. Temporarily stop the API; the UI will show a warning toast after the circuit breaker opens. Restarting the API should trigger a "Reconnected" snackbar across active sessions.

## Troubleshooting

* If the connection does not start, check the browser console/server logs for authentication errors.
* Ensure the SignalR hub endpoint is reachable over HTTPS and the client is configured with the correct base URL.
* When modifying hub contracts, update both the server interfaces and the UI event payload records located under `CommonContracts/Realtime`.

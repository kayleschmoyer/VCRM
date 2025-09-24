# Hybrid Data Source Mode

This UI can run against either the in-memory mocks or live CRM Adapter API endpoints on a per-entity basis. The hybrid pipeline is driven by the `DataSource` configuration section and the developer switch component in the layout.

## Configuration

`appsettings.json` and `appsettings.Development.json` ship with the mocks enabled by default:

```json
"DataSource": {
  "Customers": "Mock",
  "Vehicles": "Mock",
  "Invoices": "Mock",
  "Appointments": "Mock",
  "Dashboard": "Mock"
},
"Api": {
  "BaseUrl": "https://localhost:5001"
}
```

To switch an entity to the live API in configuration, set the corresponding value to `Live` or `Auto` (Auto tries the API first and automatically falls back to the mock implementation if a `HttpRequestException` occurs).

## Runtime switch

When running in the `Development` environment, the top of the shell shows a **Hybrid data source** card. Use the dropdowns to flip individual entities between **Mock**, **Live**, or **Auto** without touching code. Changes are stored in `localStorage`, applied through the `IDataSourceStrategy`, and take effect after the next refresh.

The **Reset overrides** button clears the session overrides and returns to the configuration defaults.

## Wiring live APIs later

Each `*ApiClient` class in `CRMAdapter.UI/Services/Api` already exposes the correct method signatures and route comments. Replace the TODO sections with real `HttpClient` calls that serialize the domain models expected by `CRMAdapter.Api`. The `BaseApiClient` handles JWT attachment (currently a placeholder) and is ready for Polly-based resilience policies when the endpoints go live.

> Tip: once live APIs are implemented, keep the mock services available. They power demos and ensure the UI keeps working offline.

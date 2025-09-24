# VCRM Adapter

A modular .NET 8 integration layer that exposes canonical CRM operations (customers, vehicles, invoices, appointments) while isolating backend-specific schemas through mapping files and resilient SQL access patterns.

## Table of contents
1. [Getting started](#getting-started)
2. [Annotated usage examples](#annotated-usage-examples)
3. [Troubleshooting SQL configuration issues](#troubleshooting-sql-configuration-issues)
4. [Repository layout](#repository-layout)

## Getting started

Follow this sequence the first time you set up the adapter locally.

1. **Install prerequisites.**
   - .NET SDK 8.0 or later to build `CRMAdapter.Core` and run the tests.【F:CRMAdapter/CRMAdapter.Core/CRMAdapter.Core.csproj†L1-L24】
   - A SQL Server instance reachable from your machine; the adapters open `SqlConnection` objects when run in the sample host.【F:CRMAdapter/VastOnline/SampleApp/Sample.BlazorServer/Program.cs†L6-L26】
2. **Clone and restore.**
   ```bash
   git clone <repo-url>
   cd VCRM
   dotnet restore CRMAdapter/CRMAdapter.sln
   ```
3. **Provision a database schema.** Load the tables referenced by the provided Vast Online mapping (`crm.Customer`, `crm.Vehicle`, `billing.Invoice`, etc.) so that the adapter has data to read.【F:CRMAdapter/VastOnline/Mapping/vast-online.json†L1-L33】
4. **Configure connection strings and mapping paths.**
   - When self-hosting, set environment variables for automated creation: `CRM_BACKEND` (`VAST_DESKTOP` or `VAST_ONLINE`) and optionally `CRM_MAPPING_PATH` for a custom mapping location.【F:CRMAdapter/Factory/AdapterFactory.cs†L80-L142】
   - The Blazor sample shows how to resolve the mapping file (`CRM:MappingPath`) and SQL connection string from configuration before registering the adapters in DI.【F:CRMAdapter/VastOnline/SampleApp/Sample.BlazorServer/Program.cs†L14-L26】
5. **Run automated tests to validate the setup.**
   ```bash
   dotnet test CRMAdapter/CRMAdapter.sln
   ```
6. **Host adapters in your application.** Either call `AdapterFactory.CreateFromEnvironment` for console/service scenarios or use the `AddVastOnlineAdapters` extension when integrating with ASP.NET dependency injection.【F:CRMAdapter/Factory/AdapterFactory.cs†L91-L217】

## Annotated usage examples

### Load a mapping file safely
```csharp
var fieldMap = FieldMap.LoadFromFile("CRMAdapter/VastOnline/Mapping/vast-online.json");
// LoadFromFile verifies the path, ensures the JSON exists, and parses it securely.
var customerSource = fieldMap.GetEntitySource("Customer");
// Throws a MappingConfigurationException if the mapping is missing required __source entries.
```
*Why it matters:* `FieldMap.LoadFromFile` normalizes the path, confirms the file exists, and rejects malformed JSON before adapters execute queries, preventing schema drift or injection issues from reaching the database.【F:CRMAdapter/CommonConfig/FieldMap.cs†L46-L133】

### Query a customer by identifier
```csharp
var bundle = AdapterFactory.Create(
    backend: "VAST_ONLINE",
    connectionFactory: () => new SqlConnection(connectionString),
    mappingPath: "CRMAdapter/VastOnline/Mapping/vast-online.json");

var customer = await bundle.CustomerAdapter.GetByIdAsync(customerId, cancellationToken);
```
*What happens under the hood:*
- `AdapterFactory.Create` loads the mapping, applies secure defaults (retry policy, structured logging, rate limiting), and constructs backend-specific adapters in a single bundle.【F:CRMAdapter/Factory/AdapterFactory.cs†L120-L205】【F:CRMAdapter/Factory/AdapterFactory.cs†L495-L533】
- `CustomerAdapter.GetByIdAsync` validates the identifier, projects the canonical columns defined in the mapping, and hydrates related vehicles before returning the `Customer` aggregate.【F:CRMAdapter/VastOnline/Adapter/CustomerAdapter.cs†L82-L142】

### Register adapters inside ASP.NET Core DI
```csharp
builder.Services.AddVastOnlineAdapters(
    mappingPath: configuration["CRM:MappingPath"],
    connectionFactory: _ => new SqlConnection(connectionString));
```
The extension loads the mapping once, shares it as a singleton, and wires each adapter with the configured retry, logging, and rate limiting policies for scoped consumption.【F:CRMAdapter/Factory/AdapterFactory.cs†L145-L217】

## Troubleshooting SQL configuration issues

| Symptom | Root cause | Fix |
| --- | --- | --- |
| `Mapping file '.../vast-online.json' was not found.` | `FieldMap.LoadFromFile` resolves the mapping path to an absolute location and throws when the file is missing or unreadable.【F:CRMAdapter/CommonConfig/FieldMap.cs†L52-L69】 | Double-check that the mapping file exists at the resolved path or supply `CRM_MAPPING_PATH` pointing to the correct file.【F:CRMAdapter/Factory/AdapterFactory.cs†L101-L142】 |
| `Mapping must declare at least one entity source using the '__source' suffix.` | The JSON mapping lacks required `__source` declarations per entity, so entity joins cannot be built.【F:CRMAdapter/CommonConfig/FieldMap.cs†L120-L131】 | Ensure every entity block in the mapping includes a `__source` entry referencing the fully-qualified table or view.【F:CRMAdapter/VastOnline/Mapping/vast-online.json†L5-L33】 |
| `Customer id must be provided.` when calling adapters | Guards reject empty GUIDs before issuing SQL so the retry policy does not mask invalid input.【F:CRMAdapter/VastOnline/Adapter/CustomerAdapter.cs†L109-L143】 | Confirm the caller is passing a persisted identifier and not a default/empty value. |
| Application startup throws `InvalidOperationException` about `VastOnline` connection string | The sample host refuses to run without a configured connection string, so no accidental connections are attempted.【F:CRMAdapter/VastOnline/SampleApp/Sample.BlazorServer/Program.cs†L16-L26】 | Populate the `ConnectionStrings:VastOnline` setting (user secrets, environment variables, or appsettings.json). |

## Repository layout

- `CRMAdapter.Core` – central project aggregating common configuration, contracts, infrastructure helpers, and the Vast adapter implementations.【F:CRMAdapter/CRMAdapter.Core/CRMAdapter.Core.csproj†L1-L24】
- `Factory` – factories and dependency injection helpers for assembling adapter bundles, including environment-variable aware entry points.【F:CRMAdapter/Factory/AdapterFactory.cs†L80-L533】
- `VastOnline/Mapping` – curated schema mappings for Vast Online backends shipped with the repository.【F:CRMAdapter/VastOnline/Mapping/vast-online.json†L1-L33】
- `Tests` – unit, integration, chaos, and smoke tests validating adapter behavior through the canonical interfaces.【F:CRMAdapter/CRMAdapter.sln†L1-L28】

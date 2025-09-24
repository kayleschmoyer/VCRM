# Offline Sync Architecture

The CRM Adapter UI operates in an offline-first mode to ensure the primary entities—customers, vehicles, invoices, and appointments—remain accessible even when the API is unavailable. This document outlines how the local cache, sync queue, and background worker collaborate to provide a seamless experience.

## Local Cache

- **Abstraction**: `ILocalCache` exposes CRUD methods for typed entries. `IndexedDbCache` is used for WebAssembly experiences; `FileSystemCache` backs Blazor Server / desktop deployments.
- **Structure**: Each entity type is stored in its own logical bucket, keyed by the entity identifier. The cache contains both summaries and detail projections so that list views and drill downs can hydrate instantly.
- **Updates**: Successful API calls write-through to the cache. When the API is unreachable, the cache is queried directly. Mock data is only used as a last resort if no cache entry exists.

## Sync Queue

- **Envelope**: `ChangeEnvelope` captures entity type, identifier, operation (create/update/delete), serialized payload, and timestamps.
- **Implementation**: `SyncQueue` persists the envelopes via `ILocalCache`, allowing restarts without losing work.
- **Enqueue Points**: Service methods enqueue changes whenever API mutations fail. For example, `SaveAppointmentAsync` adds a create/update record if the `POST`/`PUT` call throws `HttpRequestException`.

## Background Sync Worker

- **Configuration**: Controlled through `OfflineSync` in `appsettings.json`. The default interval is 20 seconds with the worker enabled.
- **Execution**: `BackgroundSyncWorker` retrieves pending changes, dispatches them via `ChangeDispatcher`, and updates `OfflineSyncState` with queue length, sync progress, and last successful time.
- **Conflicts**: When the API returns `409 Conflict`, the worker applies the “last write wins” policy—server versions are preserved, the queue entry is removed, and a snackbar notification is raised.

## Connectivity & UI Feedback

- `ConnectivityMonitor` listens for browser online/offline events and updates `OfflineSyncState`.
- `OfflineBanner` shows when the application is offline and reassures users that changes will sync later.
- `SyncStatus` displays queue length, last sync time, and emits snackbar messages on conflicts.

## Testing Offline Mode

1. Prime the cache by browsing the app while connected.
2. Stop or block the CRM API.
3. Refresh entity lists—data loads from the cache.
4. Perform a mutation (e.g., schedule an appointment). The UI writes to the cache immediately and the queue length increases.
5. Restore the API. Within the configured interval the background worker replays queued changes. Watch the sync status badge and application logs for confirmation.
6. Verify real-time updates arrive through SignalR once the API acknowledges the change.

## Conflict Handling

- **Detection**: A `409 Conflict` response marks the change as conflicting.
- **Resolution**: The server’s payload and timestamp are treated as canonical. The queue entry is removed, the cache is updated with the server version, and the user is informed via snackbar and console log.
- **Extensibility**: `OfflineSyncState.ReportConflict` provides a hook to replace the simple last-write-wins strategy with richer policies in the future.

## Manual Validation Checklist

- [ ] Run the UI, populate the cache (navigate across entity pages).
- [ ] Kill the API and confirm cached lists & details remain accessible.
- [ ] Submit mutations offline; observe the sync queue length growing.
- [ ] Restore connectivity and confirm queued changes replay automatically.
- [ ] Review `Docs/OfflineSync.md` for additional troubleshooting guidance.

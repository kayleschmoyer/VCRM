// DataSourceMode.cs: Enumerates the runtime data providers so entities can switch between mocks and live APIs.
namespace CRMAdapter.UI.Core.DataSource;

/// <summary>
/// Indicates which backing service should satisfy a domain contract.
/// </summary>
public enum DataSourceMode
{
    /// <summary>
    /// Only the in-memory mock service should be used.
    /// </summary>
    Mock,

    /// <summary>
    /// Prefer the live API client for the contract.
    /// </summary>
    Live,

    /// <summary>
    /// Attempt the live API first, falling back to the mock implementation when the live call fails.
    /// </summary>
    Auto
}

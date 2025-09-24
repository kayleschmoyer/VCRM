// NavigationLink.cs: Represents a routed navigation entry with role-based visibility metadata.
namespace CRMAdapter.UI.Navigation;

public sealed record NavigationLink(
    string Title,
    string Icon,
    string Href,
    string Description,
    string[] AllowedRoles);

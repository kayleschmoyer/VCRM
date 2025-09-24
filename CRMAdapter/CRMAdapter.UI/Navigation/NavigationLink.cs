// NavigationLink.cs: Represents a routed navigation entry with RBAC policy metadata.
using CRMAdapter.CommonSecurity;

namespace CRMAdapter.UI.Navigation;

public sealed record NavigationLink(
    string Title,
    string Icon,
    string Href,
    string Description,
    RbacAction RequiredAction)
{
    public string PolicyName => RbacPolicy.GetPolicyName(RequiredAction);
}

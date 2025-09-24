// JwtClientOptions.cs: Configuration contract for JWT authority and audience metadata consumed by the client.
namespace CRMAdapter.UI.Infrastructure.Security;

public sealed class JwtClientOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string MetadataAddress { get; set; } = string.Empty;
}

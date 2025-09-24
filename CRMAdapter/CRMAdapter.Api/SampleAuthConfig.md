# Sample JWT Issuance for CRMAdapter.Api

The API enforces JWT bearer authentication with role-based authorization. The defaults in
`Config/appsettings.json` expect the following token properties:

- **Issuer**: `crm-adapter-api`
- **Audience**: `crm-clients`
- **Signing key**: *(Provide via `JWT_SIGNING_KEY` environment variable or secret manager; not stored in source control.)*
- **Roles**: `Admin`, `Manager`, `Clerk`, `Tech`

## Generating a developer token

The snippet below produces a short-lived JWT that satisfies the default configuration.
It expects the signing key to be supplied at runtime—either export `JWT_SIGNING_KEY`
or load the value from your preferred secret manager before executing the script.

```powershell
# Requires PowerShell 7+
$issuer = "crm-adapter-api"
$audience = "crm-clients"
$key = $env:JWT_SIGNING_KEY
if (-not $key) {
    throw "JWT_SIGNING_KEY environment variable is not set."
}
$expires = [DateTimeOffset]::UtcNow.AddHours(4)
$claims = @(
    New-Object System.Security.Claims.Claim([System.Security.Claims.ClaimTypes]::NameIdentifier, [guid]::NewGuid().ToString()),
    New-Object System.Security.Claims.Claim([System.Security.Claims.ClaimTypes]::Role, "Admin"),
    New-Object System.Security.Claims.Claim("scope", "crm.api")
)

$credentials = New-Object Microsoft.IdentityModel.Tokens.SigningCredentials(
    (New-Object Microsoft.IdentityModel.Tokens.SymmetricSecurityKey([System.Text.Encoding]::UTF8.GetBytes($key))),
    [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::HmacSha256
)

$token = New-Object System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
    $issuer,
    $audience,
    $claims,
    [DateTime]::UtcNow,
    $expires.UtcDateTime,
    $credentials
)

$handler = New-Object System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
$handler.WriteToken($token)
```

## Mapping roles to policies

The table below shows how API policies map to roles. Ensure the JWT contains one of the
listed roles to access the corresponding endpoint.

| Policy | Allowed roles | Endpoints |
| --- | --- | --- |
| `Policies.Customers.Read` | Admin, Manager, Clerk | `GET /customers/{id}` |
| `Policies.Customers.Search` | Admin, Manager, Clerk | `POST /customers/search` |
| `Policies.Vehicles.Read` | Admin, Manager, Tech | `GET /vehicles/{id}` |
| `Policies.Invoices.Read` | Admin, Manager, Clerk | `GET /invoices/{id}` |
| `Policies.Appointments.Read` | Admin, Manager, Clerk, Tech | `GET /appointments/{id}` |

> **Production guidance:** configure `Jwt` settings to match your identity provider. Point
> `Authority` at your OpenID Connect discovery endpoint, store the symmetric key in a
> hardware security module or secret manager, and assign roles through your identity
> system rather than embedding them manually.

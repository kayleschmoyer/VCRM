// File: JwtConfig.cs
// Summary: Binds JWT authentication configuration and applies it to JwtBearerOptions.
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CRMAdapter.Api.Security;

/// <summary>
/// Represents configurable settings for JWT bearer authentication.
/// </summary>
public sealed class JwtConfig
{
    /// <summary>
    /// Name of the configuration section that holds JWT settings.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the issuer expected on incoming tokens.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets additional acceptable issuers.
    /// </summary>
    public string[]? ValidIssuers { get; set; }

    /// <summary>
    /// Gets or sets the audience expected on incoming tokens.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets additional acceptable audiences.
    /// </summary>
    public string[]? ValidAudiences { get; set; }

    /// <summary>
    /// Gets or sets the OpenID Connect authority (metadata endpoint).
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Gets or sets the symmetric signing key used for local token validation scenarios.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token issuer should be validated.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the token audience should be validated.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the token lifetime should be validated.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the signing key should be validated.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS metadata is required.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed clock skew for token expiration validation.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Applies the configuration to the supplied <see cref="JwtBearerOptions"/> instance.
    /// </summary>
    /// <param name="options">Options instance to configure.</param>
    public void Configure(JwtBearerOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.RequireHttpsMetadata = RequireHttpsMetadata;
        options.SaveToken = true;

        if (!string.IsNullOrWhiteSpace(Authority))
        {
            options.Authority = Authority;
        }

        if (!string.IsNullOrWhiteSpace(Audience))
        {
            options.Audience = Audience;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = ValidateIssuer,
            ValidateAudience = ValidateAudience,
            ValidateLifetime = ValidateLifetime,
            ValidateIssuerSigningKey = ValidateIssuerSigningKey,
            ClockSkew = ClockSkew,
            RequireSignedTokens = ValidateIssuerSigningKey,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };

        if (!string.IsNullOrWhiteSpace(Issuer))
        {
            options.TokenValidationParameters.ValidIssuer = Issuer;
        }

        if (ValidIssuers is { Length: > 0 })
        {
            options.TokenValidationParameters.ValidIssuers = ValidIssuers;
        }

        if (!string.IsNullOrWhiteSpace(Audience))
        {
            options.TokenValidationParameters.ValidAudience = Audience;
        }

        if (ValidAudiences is { Length: > 0 })
        {
            options.TokenValidationParameters.ValidAudiences = ValidAudiences;
        }

        if (!string.IsNullOrWhiteSpace(SigningKey))
        {
            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        }
    }
}

// JwtAuthProvider.cs: Handles secure JWT acquisition and refresh calls against the CRM API.
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.UI.Auth.Contracts;
using CRMAdapter.UI.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Auth;

public sealed class JwtAuthProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwtAuthProvider> _logger;
    private readonly IConfiguration _configuration;

    public JwtAuthProvider(IHttpClientFactory httpClientFactory, ILogger<JwtAuthProvider> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<JwtSession> SignInAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.CrmApi);
        var request = new AuthenticationRequest(userName, password);

        using var response = await client.PostAsJsonAsync(GetEndpoint("Authentication:Jwt:LoginEndpoint", "identity/login"), request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Authentication failed with status code {StatusCode}", response.StatusCode);
            throw new AuthenticationException("Unable to authenticate with the CRM API.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new AuthenticationException("The CRM API returned an invalid authentication payload.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresInSeconds <= 0 ? 3600 : payload.ExpiresInSeconds);
        return new JwtSession(payload.AccessToken, payload.RefreshToken ?? string.Empty, expiresAt);
    }

    public async Task<JwtSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.CrmApi);
        var request = new RefreshRequest(refreshToken);

        using var response = await client.PostAsJsonAsync(GetEndpoint("Authentication:Jwt:RefreshEndpoint", "identity/refresh"), request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Token refresh failed with status code {StatusCode}", response.StatusCode);
            throw new AuthenticationException("Unable to refresh the authentication session.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new AuthenticationException("The CRM API returned an invalid refresh payload.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresInSeconds <= 0 ? 3600 : payload.ExpiresInSeconds);
        return new JwtSession(payload.AccessToken, payload.RefreshToken ?? refreshToken, expiresAt);
    }

    private string GetEndpoint(string configurationKey, string fallback)
    {
        return _configuration[configurationKey] ?? fallback;
    }

    private sealed record AuthenticationRequest(string UserName, string Password);

    private sealed record RefreshRequest(string RefreshToken);

    private sealed record TokenResponse(string AccessToken, string? RefreshToken, int ExpiresInSeconds);
}

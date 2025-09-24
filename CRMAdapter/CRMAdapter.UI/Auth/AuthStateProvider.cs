// AuthStateProvider.cs: Supplies Blazor with JWT-backed authentication state and manages secure token storage.
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using CRMAdapter.UI.Auth.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Auth;

public sealed class AuthStateProvider : AuthenticationStateProvider
{
    private const string AccessTokenKey = "crm-adapter-access-token";
    private const string RefreshTokenKey = "crm-adapter-refresh-token";
    private const string ExpiryKey = "crm-adapter-token-expiry";

    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly JwtAuthProvider _jwtAuthProvider;
    private readonly ILogger<AuthStateProvider> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AuthStateProvider(ProtectedSessionStorage sessionStorage, JwtAuthProvider jwtAuthProvider, ILogger<AuthStateProvider> logger)
    {
        _sessionStorage = sessionStorage;
        _jwtAuthProvider = jwtAuthProvider;
        _logger = logger;
    }

    public ClaimsPrincipal CurrentUser => _currentUser;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var accessToken = await ReadTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(_currentUser);
        }

        var expiry = await ReadExpiryAsync();
        if (IsExpired(expiry))
        {
            var refreshToken = await ReadRefreshTokenAsync();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    var refreshedSession = await _jwtAuthProvider.RefreshAsync(refreshToken);
                    await StoreSessionAsync(refreshedSession);
                    accessToken = refreshedSession.AccessToken;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token refresh failed; clearing authentication state.");
                    await ClearAsync();
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    return new AuthenticationState(_currentUser);
                }
            }
            else
            {
                await ClearAsync();
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(_currentUser);
            }
        }

        var principal = BuildPrincipal(accessToken);
        _currentUser = principal;
        return new AuthenticationState(principal);
    }

    public async Task PersistSessionAsync(JwtSession session)
    {
        await StoreSessionAsync(session);
        var principal = BuildPrincipal(session.AccessToken);
        _currentUser = principal;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public async Task SignOutAsync()
    {
        await ClearAsync();
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private ClaimsPrincipal BuildPrincipal(string accessToken)
    {
        var token = _tokenHandler.ReadJwtToken(accessToken);
        var identity = new ClaimsIdentity(token.Claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }

    private Task StoreSessionAsync(JwtSession session)
    {
        return Task.WhenAll(
            _sessionStorage.SetAsync(AccessTokenKey, session.AccessToken).AsTask(),
            _sessionStorage.SetAsync(RefreshTokenKey, session.RefreshToken).AsTask(),
            _sessionStorage.SetAsync(ExpiryKey, session.ExpiresAt).AsTask());
    }

    private async Task<string?> ReadTokenAsync()
    {
        var stored = await _sessionStorage.GetAsync<string>(AccessTokenKey);
        return stored.Success ? stored.Value : null;
    }

    private async Task<string?> ReadRefreshTokenAsync()
    {
        var stored = await _sessionStorage.GetAsync<string>(RefreshTokenKey);
        return stored.Success ? stored.Value : null;
    }

    private async Task<DateTimeOffset?> ReadExpiryAsync()
    {
        var stored = await _sessionStorage.GetAsync<DateTimeOffset>(ExpiryKey);
        return stored.Success ? stored.Value : null;
    }

    private static bool IsExpired(DateTimeOffset? expiry)
    {
        return !expiry.HasValue || expiry.Value <= DateTimeOffset.UtcNow.AddMinutes(-1);
    }

    private Task ClearAsync()
    {
        return Task.WhenAll(
            _sessionStorage.DeleteAsync(AccessTokenKey).AsTask(),
            _sessionStorage.DeleteAsync(RefreshTokenKey).AsTask(),
            _sessionStorage.DeleteAsync(ExpiryKey).AsTask());
    }
}

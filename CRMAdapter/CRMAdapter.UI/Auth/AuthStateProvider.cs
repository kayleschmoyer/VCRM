// AuthStateProvider.cs: Supplies Blazor with JWT-backed authentication state and manages secure token storage.
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using CRMAdapter.UI.Auth.Contracts;
using CRMAdapter.UI.Services;
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
    private readonly EncryptedStorageService _encryptedStorage;
    private readonly JwtAuthProvider _jwtAuthProvider;
    private readonly ILogger<AuthStateProvider> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AuthStateProvider(ProtectedSessionStorage sessionStorage, EncryptedStorageService encryptedStorage, JwtAuthProvider jwtAuthProvider, ILogger<AuthStateProvider> logger)
    {
        _sessionStorage = sessionStorage;
        _encryptedStorage = encryptedStorage;
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

    /// <summary>
    /// Retrieves the currently stored access token without mutating state.
    /// </summary>
    public Task<string?> GetAccessTokenAsync()
    {
        return ReadTokenAsync();
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
            _encryptedStorage.SetAsync(AccessTokenKey, session.AccessToken),
            _encryptedStorage.SetAsync(RefreshTokenKey, session.RefreshToken),
            _sessionStorage.SetAsync(ExpiryKey, session.ExpiresAt).AsTask());
    }

    private async Task<string?> ReadTokenAsync()
    {
        return await _encryptedStorage.GetAsync(AccessTokenKey);
    }

    private async Task<string?> ReadRefreshTokenAsync()
    {
        return await _encryptedStorage.GetAsync(RefreshTokenKey);
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
            _encryptedStorage.DeleteAsync(AccessTokenKey),
            _encryptedStorage.DeleteAsync(RefreshTokenKey),
            _sessionStorage.DeleteAsync(ExpiryKey).AsTask());
    }
}

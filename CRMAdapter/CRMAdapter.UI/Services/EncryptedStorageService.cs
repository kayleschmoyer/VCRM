// EncryptedStorageService.cs: Wraps protected browser storage with AES-GCM encryption.
using System;
using System.Security;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using CRMAdapter.UI.Services.Diagnostics;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.UI.Services;

public sealed class EncryptedStorageService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly DataProtector _dataProtector;
    private readonly CorrelationContext _correlationContext;
    private readonly ILogger<EncryptedStorageService> _logger;

    public EncryptedStorageService(
        ProtectedSessionStorage sessionStorage,
        DataProtector dataProtector,
        CorrelationContext correlationContext,
        ILogger<EncryptedStorageService> logger)
    {
        _sessionStorage = sessionStorage ?? throw new ArgumentNullException(nameof(sessionStorage));
        _dataProtector = dataProtector ?? throw new ArgumentNullException(nameof(dataProtector));
        _correlationContext = correlationContext ?? throw new ArgumentNullException(nameof(correlationContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetAsync(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var correlationId = _correlationContext.CurrentCorrelationId;
        var encrypted = _dataProtector.Encrypt(value, correlationId);
        await _sessionStorage.SetAsync(key, encrypted).AsTask().ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var stored = await _sessionStorage.GetAsync<string>(key).ConfigureAwait(false);
        if (!stored.Success || string.IsNullOrWhiteSpace(stored.Value))
        {
            return null;
        }

        var correlationId = _correlationContext.CurrentCorrelationId;
        try
        {
            return _dataProtector.Decrypt(stored.Value!, correlationId);
        }
        catch (SecurityException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt stored value for key {Key} and correlation {CorrelationId}.", key, correlationId);
            await _sessionStorage.DeleteAsync(key).AsTask().ConfigureAwait(false);
            return null;
        }
    }

    public Task DeleteAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        return _sessionStorage.DeleteAsync(key).AsTask();
    }
}

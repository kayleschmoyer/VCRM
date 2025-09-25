// DataProtector.cs: Provides AES-256-GCM encryption utilities for sensitive data.
using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Encrypts and decrypts sensitive payloads using AES-256-GCM with key rotation support.
/// </summary>
public sealed class DataProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly SecuritySettings _settings;
    private readonly ResolvedSecrets _resolvedSecrets;
    private readonly ILogger<DataProtector> _logger;

    public DataProtector(SecuritySettings settings, ResolvedSecrets resolvedSecrets, ILogger<DataProtector> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _resolvedSecrets = resolvedSecrets ?? throw new ArgumentNullException(nameof(resolvedSecrets));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Encrypts plaintext using the active encryption key.
    /// </summary>
    public string Encrypt(string plaintext, string correlationId)
    {
        if (plaintext is null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        var keyId = _settings.EncryptionKeyId;
        if (!_resolvedSecrets.EncryptionKeys.TryGetValue(keyId, out var keyBytes))
        {
            throw new SecurityException($"Encryption key '{keyId}' is not available.");
        }

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(keyBytes);
            aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);
        }
        catch (CryptographicException ex)
        {
            throw new SecurityException($"Encryption failure for correlation {correlationId}.", ex);
        }

        var payload = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length + cipherBytes.Length, tag.Length);

        var envelope = string.Concat(keyId, ":", Convert.ToBase64String(payload));
        _logger.LogDebug("Encrypted payload using key {KeyId} for correlation {CorrelationId}.", keyId, correlationId);
        return envelope;
    }

    /// <summary>
    /// Decrypts a payload previously encrypted by <see cref="Encrypt"/>.
    /// </summary>
    public string Decrypt(string encryptedPayload, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(encryptedPayload))
        {
            throw new ArgumentNullException(nameof(encryptedPayload));
        }

        var separatorIndex = encryptedPayload.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new SecurityException($"Encrypted payload is missing key metadata for correlation {correlationId}.");
        }

        var keyId = encryptedPayload[..separatorIndex];
        var encoded = encryptedPayload[(separatorIndex + 1)..];

        if (!_resolvedSecrets.EncryptionKeys.TryGetValue(keyId, out var keyBytes))
        {
            _logger.LogWarning("Encrypted payload referenced unknown key {KeyId} for correlation {CorrelationId}.", keyId, correlationId);
            throw new SecurityException($"Unknown encryption key '{keyId}'.");
        }

        byte[] payloadBytes;
        try
        {
            payloadBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new SecurityException($"Encrypted payload is not valid base64 for correlation {correlationId}.", ex);
        }

        if (payloadBytes.Length < NonceSize + TagSize)
        {
            throw new SecurityException($"Encrypted payload is truncated for correlation {correlationId}.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherLength = payloadBytes.Length - NonceSize - TagSize;
        var cipherBytes = new byte[cipherLength];

        Buffer.BlockCopy(payloadBytes, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(payloadBytes, NonceSize, cipherBytes, 0, cipherLength);
        Buffer.BlockCopy(payloadBytes, NonceSize + cipherLength, tag, 0, TagSize);

        var plaintextBytes = new byte[cipherLength];

        try
        {
            using var aes = new AesGcm(keyBytes);
            aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        }
        catch (CryptographicException ex)
        {
            throw new SecurityException($"Decryption failure for correlation {correlationId}.", ex);
        }

        _logger.LogDebug("Decrypted payload using key {KeyId} for correlation {CorrelationId}.", keyId, correlationId);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}

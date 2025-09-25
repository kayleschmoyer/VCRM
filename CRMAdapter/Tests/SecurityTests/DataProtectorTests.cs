using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.Tests.SecurityTests;

public sealed class DataProtectorTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var keyId = "TEST_KEY";
        var securitySettings = new SecuritySettings
        {
            EncryptionKeyId = keyId,
            Secrets = new SecretNameSettings
            {
                JwtSigningKey = "JWT_KEY",
                Sql = new SqlSecretNames { VastDesktop = "SQL_DESKTOP", VastOnline = "SQL_ONLINE", Audit = "SQL_AUDIT" },
            },
        };

        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var secrets = new ResolvedSecrets(
            jwtSigningKey: "jwt-secret",
            sqlConnections: new Dictionary<string, string> { ["VastDesktop"] = "Server=.;", ["VastOnline"] = "Server=.;", ["Audit"] = "Server=.;" },
            apiCredentials: new Dictionary<string, string>(),
            encryptionKeys: new Dictionary<string, byte[]> { [keyId] = keyBytes });

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var protector = new DataProtector(securitySettings, secrets, loggerFactory.CreateLogger<DataProtector>());

        const string plaintext = "Highly Sensitive Token";
        var correlation = Guid.NewGuid().ToString();

        var encrypted = protector.Encrypt(plaintext, correlation);
        encrypted.Should().NotBeNullOrWhiteSpace();

        var roundTrip = protector.Decrypt(encrypted, correlation);
        roundTrip.Should().Be(plaintext);
    }
}

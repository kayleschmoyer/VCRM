using System;
using System.Collections.Generic;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMAdapter.Tests.SecurityTests;

public sealed class SqlAuditSinkTests
{
    [Fact]
    public void DecryptPayload_RestoresOriginalValue()
    {
        var settings = new SecuritySettings
        {
            EncryptionKeyId = "CRM_ENCRYPTION_KEY_CURRENT",
            Secrets = new SecretNameSettings
            {
                JwtSigningKey = "CRM_JWT_SIGNING_KEY",
                Sql = new SqlSecretNames
                {
                    VastDesktop = "CRM_SQL_VAST_DESKTOP",
                    VastOnline = "CRM_SQL_VAST_ONLINE",
                    Audit = "CRM_SQL_AUDIT",
                },
            },
        };

        var resolvedSecrets = new ResolvedSecrets(
            jwtSigningKey: "jwt-secret",
            sqlConnections: new Dictionary<string, string>
            {
                ["VastDesktop"] = "Server=.;Encrypt=True;",
                ["VastOnline"] = "Server=.;Encrypt=True;",
                ["Audit"] = "Server=.;Encrypt=True;",
            },
            apiCredentials: new Dictionary<string, string>(),
            encryptionKeys: new Dictionary<string, byte[]>
            {
                ["CRM_ENCRYPTION_KEY_CURRENT"] = new byte[32],
            });

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var protector = new DataProtector(settings, resolvedSecrets, loggerFactory.CreateLogger<DataProtector>());
        var options = Options.Create(new AuditSettings
        {
            Sql = new AuditSettings.SqlSinkSettings
            {
                ConnectionString = "Server=.;Encrypt=True;",
            },
        });

        var sink = new SqlAuditSink(options, resolvedSecrets, protector, loggerFactory.CreateLogger<SqlAuditSink>());
        var correlationId = Guid.NewGuid().ToString();
        var encrypted = protector.Encrypt("payload", correlationId);
        var decrypted = sink.DecryptPayload(encrypted, correlationId);

        decrypted.Should().Be("payload");
    }
}

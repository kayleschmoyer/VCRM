using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.Tests.SecurityTests;

public sealed class StartupGuardTests
{
    [Fact]
    public async Task Bootstrap_Fails_WhenSigningKeyMissing()
    {
        var encryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        Environment.SetEnvironmentVariable("CRM_SQL_VAST_DESKTOP", "Server=.;Encrypt=True;TrustServerCertificate=False;");
        Environment.SetEnvironmentVariable("CRM_SQL_VAST_ONLINE", "Server=.;Encrypt=True;TrustServerCertificate=False;");
        Environment.SetEnvironmentVariable("CRM_SQL_AUDIT", "Server=.;Encrypt=True;TrustServerCertificate=False;");
        Environment.SetEnvironmentVariable("CRM_ENCRYPTION_KEY_CURRENT", encryptionKey);
        Environment.SetEnvironmentVariable("CRM_JWT_SIGNING_KEY", null);

        try
        {
            var configValues = new Dictionary<string, string?>
            {
                ["Security:UseKeyVault"] = "false",
                ["Security:EncryptionKeyId"] = "CRM_ENCRYPTION_KEY_CURRENT",
                ["Security:Secrets:JwtSigningKey"] = "CRM_JWT_SIGNING_KEY",
                ["Security:Secrets:Sql:VastDesktop"] = "CRM_SQL_VAST_DESKTOP",
                ["Security:Secrets:Sql:VastOnline"] = "CRM_SQL_VAST_ONLINE",
                ["Security:Secrets:Sql:Audit"] = "CRM_SQL_AUDIT",
                ["Security:Secrets:Api:ClientId"] = "CRM_API_CLIENT_ID",
                ["Security:Secrets:Api:ClientSecret"] = "CRM_API_CLIENT_SECRET",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
            var environment = new TestHostEnvironment();

            Func<Task> act = () => SecurityBootstrapper.InitializeAsync(configuration, environment, loggerFactory);

            await act.Should().ThrowAsync<System.Security.SecurityException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CRM_SQL_VAST_DESKTOP", null);
            Environment.SetEnvironmentVariable("CRM_SQL_VAST_ONLINE", null);
            Environment.SetEnvironmentVariable("CRM_SQL_AUDIT", null);
            Environment.SetEnvironmentVariable("CRM_ENCRYPTION_KEY_CURRENT", null);
            Environment.SetEnvironmentVariable("CRM_JWT_SIGNING_KEY", null);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CRMAdapter.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    }
}

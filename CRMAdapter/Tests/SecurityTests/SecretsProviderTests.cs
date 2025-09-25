using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.Tests.SecurityTests;

public sealed class SecretsProviderTests
{
    [Fact]
    public async Task EnvSecretsProvider_ResolvesConfiguredValues()
    {
        const string secretName = "UNIT_TEST_SECRET";
        Environment.SetEnvironmentVariable(secretName, "super-secret-value");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var provider = new EnvSecretsProvider(loggerFactory.CreateLogger<EnvSecretsProvider>());

        try
        {
            var resolved = await provider.GetSecretAsync(secretName);
            resolved.Should().Be("super-secret-value");

            var batch = await provider.GetSecretsAsync(new[] { secretName });
            batch.Should().ContainKey(secretName);
            batch[secretName].Should().Be("super-secret-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    [Fact]
    public async Task VaultSecretsProvider_UsesAdapter()
    {
        var backingStore = new Dictionary<string, string>
        {
            ["sample-secret"] = "value-from-vault",
        };

        var adapter = new TestVaultAdapter(backingStore);
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var provider = new VaultSecretsProvider(adapter, loggerFactory.CreateLogger<VaultSecretsProvider>());

        var resolved = await provider.GetSecretAsync("sample-secret");
        resolved.Should().Be("value-from-vault");
    }

    private sealed class TestVaultAdapter : IVaultAdapter
    {
        private readonly IDictionary<string, string> _store;

        public TestVaultAdapter(IDictionary<string, string> store)
        {
            _store = store;
        }

        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
        {
            _store.TryGetValue(name, out var value);
            return Task.FromResult<string?>(value);
        }
    }
}

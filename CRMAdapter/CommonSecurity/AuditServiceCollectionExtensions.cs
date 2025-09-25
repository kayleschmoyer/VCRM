// AuditServiceCollectionExtensions.cs: Dependency injection helpers for audit logging infrastructure.
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CRMAdapter.CommonSecurity;

/// <summary>
/// Extension methods for wiring audit logging components into DI containers.
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="AuditLogger"/> and configured sink using application configuration.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configuration">Configuration root used to resolve <see cref="AuditSettings"/>.</param>
    /// <returns>The supplied service collection for chaining.</returns>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services.Configure<AuditSettings>(configuration.GetSection(AuditSettings.SectionName));
        services.AddSingleton<AuditLogger>();

        services.AddSingleton<IAuditSink>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AuditSettings>>();
            var settings = options.Value ?? new AuditSettings();
            var sinkName = settings.Sink?.Trim();
            return sinkName?.Equals(AuditSinkNames.File, StringComparison.OrdinalIgnoreCase) == true
                ? provider.GetRequiredService<FileAuditSink>()
                : sinkName?.Equals(AuditSinkNames.Sql, StringComparison.OrdinalIgnoreCase) == true
                    ? provider.GetRequiredService<SqlAuditSink>()
                    : provider.GetRequiredService<ConsoleAuditSink>();
        });

        services.AddSingleton<FileAuditSink>();
        services.AddSingleton<SqlAuditSink>();
        services.AddSingleton<ConsoleAuditSink>();

        return services;
    }
}

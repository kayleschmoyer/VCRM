// File: Startup.cs
// Summary: Configures services and the middleware pipeline for the CRMAdapter.Api minimal API host.
using System;
using System.Data.Common;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Security;
using CRMAdapter.Api.Endpoints;
using CRMAdapter.Api.Events;
using CRMAdapter.Api.Hubs;
using CRMAdapter.Api.Middleware;
using CRMAdapter.Api.Security;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonInfrastructure;
using CRMAdapter.Factory;
using CRMAdapter.CommonSecurity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace CRMAdapter.Api;

/// <summary>
/// Configures dependency injection, security, adapters, and middleware for the API host.
/// </summary>
public sealed class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ResolvedSecrets _resolvedSecrets;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">Configuration root used to populate options.</param>
    /// <param name="environment">Environment metadata for conditional behavior.</param>
    /// <param name="resolvedSecrets">Secret material resolved during bootstrap.</param>
    public Startup(IConfiguration configuration, IHostEnvironment environment, ResolvedSecrets resolvedSecrets)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _resolvedSecrets = resolvedSecrets ?? throw new ArgumentNullException(nameof(resolvedSecrets));
    }

    /// <summary>
    /// Registers services required by the API.
    /// </summary>
    /// <param name="services">Service collection to populate.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddSingleton(_resolvedSecrets);
        services.AddOptions();
        services.AddHttpContextAccessor();
        services.AddAuditLogging(_configuration);
        services.Configure<JwtConfig>(_configuration.GetSection(JwtConfig.SectionName));

        var jwtConfig = _configuration.GetSection(JwtConfig.SectionName).Get<JwtConfig>() ?? new JwtConfig();
        jwtConfig.Authority = jwtConfig.Authority?.Trim();
        jwtConfig.SigningKey = _resolvedSecrets.JwtSigningKey?.Trim();

        if (!_environment.IsDevelopment())
        {
            jwtConfig.RequireHttpsMetadata = true;
            if (!string.IsNullOrWhiteSpace(jwtConfig.Authority)
                && !jwtConfig.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("JWT authority endpoints must use HTTPS outside of development environments.");
            }
        }

        if (string.IsNullOrWhiteSpace(jwtConfig.SigningKey) && string.IsNullOrWhiteSpace(jwtConfig.Authority))
        {
            throw new SecurityException("JWT authentication requires either an Authority or a SigningKey supplied via a secure secret store.");
        }

        services.AddSingleton(jwtConfig);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => jwtConfig.Configure(options));

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var rbacMatrix = RbacPolicy.LoadAsync(_environment).GetAwaiter().GetResult();
        services.AddSingleton(rbacMatrix);
        services.AddSingleton<IRbacAuthorizationService, RbacAuthorizationService>();
        services.AddAuthorization(options => RbacPolicy.RegisterPolicies(options, rbacMatrix));
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);

        RegisterAdapterBundle(services);

        services.AddScoped(provider => provider.GetRequiredService<AdapterBundle>().CustomerAdapter);
        services.AddScoped(provider => provider.GetRequiredService<AdapterBundle>().VehicleAdapter);
        services.AddScoped(provider => provider.GetRequiredService<AdapterBundle>().InvoiceAdapter);
        services.AddScoped(provider => provider.GetRequiredService<AdapterBundle>().AppointmentAdapter);

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = _environment.IsDevelopment();
        });
    }

    /// <summary>
    /// Wires middleware and endpoint mappings into the pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public void Configure(WebApplication app)
    {
        if (app is null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityGuardMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseSerilogRequestLogging();

        if (_environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAuthentication();
        app.UseMiddleware<AuditMiddleware>();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CRM Adapter API v1");
            options.DisplayRequestDuration();
        });

        app.MapCustomersEndpoints();
        app.MapVehiclesEndpoints();
        app.MapInvoicesEndpoints();
        app.MapAppointmentsEndpoints();
        app.MapHub<CrmEventsHub>("/crmhub");

        EventDispatcher.Configure(app.Services);
    }

    private void ConfigureSwagger(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        var info = new OpenApiInfo
        {
            Title = "CRM Adapter API",
            Version = "v1",
            Description = "Canonical CRM endpoints backed by adapter abstractions.",
        };
        options.SwaggerDoc("v1", info);

        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Docs", "openapi.xml");
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }

        var securityScheme = new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer",
            },
        };

        options.AddSecurityDefinition("Bearer", securityScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { securityScheme, Array.Empty<string>() },
        });
    }

    private void RegisterAdapterBundle(IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var backend = ResolveBackend();
            var connectionFactory = CreateConnectionFactory(backend);
            var mappingPath = ResolveMappingPath(backend);
            var factoryOptions = BuildAdapterFactoryOptions(backend);
            return AdapterFactory.Create(backend, connectionFactory, mappingPath, factoryOptions);
        });
    }

    private AdapterFactoryOptions BuildAdapterFactoryOptions(string backend)
    {
        var maxConcurrency = _configuration.GetValue<int?>("CRM:Adapters:MaxConcurrency") ?? 16;
        var options = AdapterFactoryOptions.SecureDefaults(null, maxConcurrency);
        options.Logger = ResolveAdapterLogger(backend);
        return options;
    }

    private IAdapterLogger ResolveAdapterLogger(string backend)
    {
        var normalizedBackend = backend.Trim().ToUpperInvariant();
        if (string.Equals(normalizedBackend, "VAST_ONLINE", StringComparison.Ordinal))
        {
            var connectionString = _configuration["Logging:ApplicationInsights:ConnectionString"]
                ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return NullAdapterLogger.Instance;
            }

            var roleName = _configuration["Logging:ApplicationInsights:RoleName"]
                ?? Environment.GetEnvironmentVariable("CRM_APPINSIGHTS_ROLE");
            return AdapterLoggerFactory.CreateOnlineLogger(connectionString!, roleName);
        }

        var source = _configuration["Logging:EventLog:Source"]
            ?? Environment.GetEnvironmentVariable("CRM_EVENT_SOURCE")
            ?? "CRMAdapter.Api";
        var logName = _configuration["Logging:EventLog:LogName"]
            ?? Environment.GetEnvironmentVariable("CRM_EVENT_LOG")
            ?? "Application";
        return AdapterLoggerFactory.CreateDesktopLogger(source, logName);
    }

    private Func<DbConnection> CreateConnectionFactory(string backend)
    {
        var connectionString = ResolveConnectionString(backend);
        return () => new SqlConnection(connectionString);
    }

    private string ResolveConnectionString(string backend)
    {
        var connectionName = string.Equals(backend.Trim(), "VAST_ONLINE", StringComparison.OrdinalIgnoreCase)
            ? "VastOnline"
            : "VastDesktop";

        if (!_resolvedSecrets.SqlConnections.TryGetValue(connectionName, out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
        {
            throw new SecurityException($"Connection string '{connectionName}' is not configured.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            Encrypt = true,
            TrustServerCertificate = false,
        };

        return builder.ConnectionString;
    }

    private string ResolveMappingPath(string backend)
    {
        var environmentOverride = Environment.GetEnvironmentVariable("CRM_MAPPING_PATH");
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return Path.GetFullPath(environmentOverride!);
        }

        var mappingKey = $"CRM:Mapping:{backend.Trim().ToUpperInvariant()}";
        var mappingPath = _configuration[mappingKey];
        if (string.IsNullOrWhiteSpace(mappingPath))
        {
            throw new InvalidOperationException($"Mapping path for backend '{backend}' is not configured.");
        }

        var combined = Path.Combine(AppContext.BaseDirectory, mappingPath!);
        return Path.GetFullPath(combined);
    }

    private string ResolveBackend()
    {
        var backendFromConfig = _configuration["CRM:Backend"];
        if (!string.IsNullOrWhiteSpace(backendFromConfig))
        {
            return backendFromConfig!;
        }

        var backendFromEnvironment = Environment.GetEnvironmentVariable("CRM_BACKEND");
        return string.IsNullOrWhiteSpace(backendFromEnvironment) ? "VAST_DESKTOP" : backendFromEnvironment!;
    }
}

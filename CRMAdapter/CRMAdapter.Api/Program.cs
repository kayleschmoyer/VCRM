// File: Program.cs
// Summary: Entry point for the CRMAdapter.Api application that wires up the minimal API pipeline.
using System.IO;
using CRMAdapter.Api.Logging;
using CRMAdapter.CommonSecurity;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var commonConfigPath = Path.Combine(builder.Environment.ContentRootPath, "..", "CommonConfig");
builder.Configuration.AddJsonFile(Path.Combine(commonConfigPath, "AuditSettings.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(commonConfigPath, "SecuritySettings.json"), optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile(Path.Combine(commonConfigPath, "RateLimitSettings.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "Config", "AuditSettings.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "Config", "RateLimitSettings.json"), optional: true, reloadOnChange: true);

using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var securityBootstrap = await SecurityBootstrapper.InitializeAsync(builder.Configuration, builder.Environment, bootstrapLoggerFactory);

builder.Services.AddSingleton(securityBootstrap.Settings);
builder.Services.AddSingleton(securityBootstrap.Secrets);
builder.Services.AddSingleton<ISecretsProvider>(_ => securityBootstrap.Provider);
builder.Services.AddSingleton<DataProtector>();
builder.Services.AddSingleton<SecretsResolver>();

SerilogConfig.Configure(builder);

var startup = new CRMAdapter.Api.Startup(builder.Configuration, builder.Environment, securityBootstrap.Secrets);
startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app);

app.Run();

/// <summary>
/// Marker class used by integration tests to reference the application entry point.
/// </summary>
public partial class Program
{
}

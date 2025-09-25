// File: Program.cs
// Summary: Entry point for the CRMAdapter.Api application that wires up the minimal API pipeline.
using System.IO;
using CRMAdapter.Api.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "Config", "AuditSettings.json"), optional: true, reloadOnChange: true);

SerilogConfig.Configure(builder);

var startup = new CRMAdapter.Api.Startup(builder.Configuration, builder.Environment);
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

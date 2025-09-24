/*
 * File: Program.cs
 * Role: Demonstrates integrating Vast Online adapters into a Blazor Server application.
 * Architectural Purpose: Showcases dependency injection registration and usage of the schema-agnostic adapters.
 */
using System;
using System.Data.Common;
using System.Data.SqlClient;
using CRMAdapter.Factory;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

var mappingPath = builder.Configuration["CRM:MappingPath"] ?? "CRMAdapter/VastOnline/Mapping/vast-online.json";
var connectionString = builder.Configuration.GetConnectionString("VastOnline")
    ?? throw new InvalidOperationException("Configure the VastOnline connection string in appsettings.json.");

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddVastOnlineAdapters(mappingPath, _ =>
{
    DbConnection connection = new SqlConnection(connectionString);
    return connection;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

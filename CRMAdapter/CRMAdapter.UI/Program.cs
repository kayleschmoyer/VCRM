// Program.cs: Configures dependency injection, security, and MudBlazor services for the CRM Adapter UI.
using System.Net.Http.Headers;
using CRMAdapter.UI.Auth;
using CRMAdapter.UI.Hosting;
using CRMAdapter.UI.Infrastructure.Security;
using CRMAdapter.UI.Navigation;
using CRMAdapter.UI.Services.Customers;
using CRMAdapter.UI.Services.Diagnostics;
using CRMAdapter.UI.Theming;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.IdentityModel.Tokens;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions();
builder.Services.AddAuthorization(options => RolePolicies.RegisterPolicies(options));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["Authentication:Jwt:Authority"];
        var audience = builder.Configuration["Authentication:Jwt:Audience"];
        var metadataAddress = builder.Configuration["Authentication:Jwt:MetadataAddress"];

        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
            options.TokenValidationParameters.ValidIssuer = authority;
            options.TokenValidationParameters.ValidateIssuer = true;
        }
        else
        {
            options.TokenValidationParameters.ValidateIssuer = false;
        }

        if (!string.IsNullOrWhiteSpace(audience))
        {
            options.Audience = audience;
            options.TokenValidationParameters.ValidAudience = audience;
            options.TokenValidationParameters.ValidateAudience = true;
        }
        else
        {
            options.TokenValidationParameters.ValidateAudience = false;
        }

        if (!string.IsNullOrWhiteSpace(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
        }

        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<JwtAuthProvider>();
builder.Services.AddSingleton<NavigationMenuService>();
builder.Services.AddScoped<AppThemeState>();
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddSingleton<ICustomerDirectory, InMemoryCustomerDirectory>();

builder.Services.AddHttpClient(HttpClientNames.CrmApi, client =>
    {
        var baseAddress = builder.Configuration["RemoteApis:CrmApi:BaseUri"] ?? "https://localhost:7150/";
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    });

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<ApplicationHost>()
    .AddInteractiveServerRenderMode();

app.Run();

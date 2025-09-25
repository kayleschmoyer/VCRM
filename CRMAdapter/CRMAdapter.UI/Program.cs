// Program.cs: Configures dependency injection, security, and MudBlazor services for the CRM Adapter UI.
using System.IO;
using System.Net.Http.Headers;
using CRMAdapter.CommonSecurity;
using CRMAdapter.UI.Auth;
using CRMAdapter.UI.Core.DataSource;
using CRMAdapter.UI.Core.Storage;
using CRMAdapter.UI.Core.Sync;
using CRMAdapter.UI.Hosting;
using CRMAdapter.UI.Infrastructure.Security;
using CRMAdapter.UI.Navigation;
using CRMAdapter.UI.Services.Api.Appointments;
using CRMAdapter.UI.Services.Api.Customers;
using CRMAdapter.UI.Services.Api.Dashboard;
using CRMAdapter.UI.Services.Api.Invoices;
using CRMAdapter.UI.Services.Api.Vehicles;
using CRMAdapter.UI.Services.Contracts;
using CRMAdapter.UI.Services.Diagnostics;
using CRMAdapter.UI.Services.Mock.Appointments;
using CRMAdapter.UI.Services.Mock.Customers;
using CRMAdapter.UI.Services.Mock.Dashboard;
using CRMAdapter.UI.Services.Mock.Invoices;
using CRMAdapter.UI.Services.Mock.Vehicles;
using CRMAdapter.UI.Services.Realtime;
using CRMAdapter.UI.Theming;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var commonConfigPath = Path.Combine(builder.Environment.ContentRootPath, "..", "CommonConfig");
builder.Configuration.AddJsonFile(Path.Combine(commonConfigPath, "SecuritySettings.json"), optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile(Path.Combine(commonConfigPath, "AuditSettings.json"), optional: true, reloadOnChange: true);
using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var securityBootstrap = await SecurityBootstrapper.InitializeAsync(builder.Configuration, builder.Environment, bootstrapLoggerFactory);

builder.Services.AddSingleton(securityBootstrap.Settings);
builder.Services.AddSingleton(securityBootstrap.Secrets);
builder.Services.AddSingleton<ISecretsProvider>(_ => securityBootstrap.Provider);
builder.Services.AddSingleton<DataProtector>();
builder.Services.AddSingleton<SecretsResolver>();

builder.Configuration.AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "Config", "AuditSettings.json"), optional: true, reloadOnChange: true);

builder.Services.AddOptions();
var rbacMatrix = RbacPolicy.LoadAsync(builder.Environment).GetAwaiter().GetResult();
builder.Services.AddSingleton(rbacMatrix);
builder.Services.AddSingleton<IRbacAuthorizationService, RbacAuthorizationService>();
builder.Services.AddAuthorization(options => RbacPolicy.RegisterPolicies(options, rbacMatrix));
builder.Services.AddAuditLogging(builder.Configuration);

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
builder.Services.AddScoped<EncryptedStorageService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<JwtAuthProvider>();
builder.Services.AddSingleton<NavigationMenuService>();
builder.Services.AddScoped<AppThemeState>();
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddTransient<CorrelationDelegatingHandler>();
builder.Services.Configure<DataSourceOptions>(builder.Configuration.GetSection("DataSource"));
builder.Services.Configure<OfflineSyncOptions>(builder.Configuration.GetSection(OfflineSyncOptions.SectionName));

builder.Services.AddSingleton<OfflineSyncState>();

if (OperatingSystem.IsBrowser())
{
    builder.Services.AddScoped<ILocalCache, IndexedDbCache>();
}
else
{
    builder.Services.AddSingleton<ILocalCache>(sp =>
    {
        var environment = sp.GetRequiredService<IHostEnvironment>();
        var cachePath = Path.Combine(environment.ContentRootPath, "offline-cache");
        return new FileSystemCache(cachePath);
    });
}

builder.Services.AddSingleton<ISyncQueue, SyncQueue>();
builder.Services.AddSingleton<IChangeDispatcher, ChangeDispatcher>();
builder.Services.AddHostedService<BackgroundSyncWorker>();
builder.Services.AddScoped<ConnectivityMonitor>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001";
var apiBaseAddress = new Uri(apiBaseUrl);

void ConfigureCrmClient(HttpClient client)
{
    client.BaseAddress = apiBaseAddress;
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}

builder.Services.AddHttpClient(HttpClientNames.CrmApi, ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();
builder.Services.AddHttpClient<CustomerApiClient>(ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();
builder.Services.AddHttpClient<VehicleApiClient>(ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();
builder.Services.AddHttpClient<InvoiceApiClient>(ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();
builder.Services.AddHttpClient<AppointmentApiClient>(ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();
builder.Services.AddHttpClient<DashboardApiClient>(ConfigureCrmClient)
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();

builder.Services.AddScoped<InMemoryCustomerDirectory>();
builder.Services.AddScoped<InMemoryVehicleRegistry>();
builder.Services.AddScoped<InMemoryInvoiceWorkspace>();
builder.Services.AddScoped<InMemoryAppointmentBook>();
builder.Services.AddScoped<InMemoryDashboardAnalytics>();

builder.Services.AddScoped<IDataSourceStrategy, DataSourceStrategy>();
builder.Services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<ICustomerService>());
builder.Services.AddScoped<IVehicleService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IVehicleService>());
builder.Services.AddScoped<IInvoiceService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IInvoiceService>());
builder.Services.AddScoped<IAppointmentService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IAppointmentService>());
builder.Services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<IDataSourceStrategy>().GetService<IDashboardService>());

builder.Services.AddSingleton<IHubConnectionProxyFactory, SignalRHubConnectionProxyFactory>();
builder.Services.AddScoped<RealtimeHubConnection>();
builder.Services.AddScoped<CustomerRealtimeService>();
builder.Services.AddScoped<InvoiceRealtimeService>();
builder.Services.AddScoped<VehicleRealtimeService>();
builder.Services.AddScoped<AppointmentRealtimeService>();

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

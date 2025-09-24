// ApiPolicyEnforcementTests.cs: Exercises API endpoints to validate RBAC policy enforcement.
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonDomain;
using CRMAdapter.CommonSecurity;
using CRMAdapter.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CRMAdapter.Tests.RbacTests;

public sealed class ApiPolicyEnforcementTests : IAsyncLifetime
{
    private const string TestSigningKey = "RbacPolicyTestSigningKey123!";
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient? _client;
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _invoiceId = Guid.NewGuid();

    public ApiPolicyEnforcementTests()
    {
        var customer = CreateCustomer(_customerId);
        var bundle = new AdapterBundle(
            new TestCustomerAdapter(customer),
            new TestVehicleAdapter(customer),
            new TestInvoiceAdapter(_invoiceId),
            new TestAppointmentAdapter(customer));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = TestSigningKey,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AdapterBundle>();
                services.AddSingleton(bundle);
            });
        });
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetInvoice_WithUnauthorizedRole_ReturnsForbidden()
    {
        if (_client is null)
        {
            throw new InvalidOperationException("The HTTP client is not initialized.");
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(RbacRole.Tech));
        var response = await _client.GetAsync($"/invoices/{_invoiceId}").ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCustomer_WithUnknownRole_ReturnsForbidden()
    {
        if (_client is null)
        {
            throw new InvalidOperationException("The HTTP client is not initialized.");
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(null));
        var response = await _client.GetAsync($"/customers/{_customerId}").ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string GenerateToken(RbacRole? role)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var handler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        };

        if (role.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Value.ToString()));
        }

        var token = handler.CreateJwtSecurityToken(
            issuer: "crm-adapter-api",
            audience: "crm-clients",
            subject: new ClaimsIdentity(claims),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    private static Customer CreateCustomer(Guid id)
    {
        var address = new PostalAddress("123 Main St", null, "Columbus", "OH", "43004", "US");
        var vehicleReference = new VehicleReference(Guid.NewGuid(), "1FTSW21R08EB53158");
        return new Customer(id, "Test User", "test@example.com", "+16145550123", address, new[] { vehicleReference });
    }

    private sealed class TestCustomerAdapter : ICustomerAdapter
    {
        private readonly Customer _customer;

        public TestCustomerAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Customer?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Customer?>(id == _customer.Id ? _customer : null);
        }

        public Task<IReadOnlyCollection<Customer>> SearchByNameAsync(string nameQuery, int maxResults, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Customer>>(new[] { _customer });
        }

        public Task<IReadOnlyCollection<Customer>> GetRecentCustomersAsync(int maxResults, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Customer>>(new[] { _customer });
        }
    }

    private sealed class TestVehicleAdapter : IVehicleAdapter
    {
        private readonly Customer _customer;

        public TestVehicleAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Vehicle?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            var reference = _customer.Vehicles.First();
            var vehicle = new Vehicle(reference.Id, reference.Vin, 2022, "Tesla", "Model Y", "Active", Array.Empty<ServiceHistoryEntry>());
            return Task.FromResult<Vehicle?>(vehicle);
        }
    }

    private sealed class TestInvoiceAdapter : IInvoiceAdapter
    {
        private readonly Guid _invoiceId;

        public TestInvoiceAdapter(Guid invoiceId)
        {
            _invoiceId = invoiceId;
        }

        public Task<Invoice?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            if (id != _invoiceId)
            {
                return Task.FromResult<Invoice?>(null);
            }

            var invoice = new Invoice(id, "INV-1001", DateTime.UtcNow, "USD", 1250m, 0m, "Paid", Guid.NewGuid(), Guid.NewGuid());
            return Task.FromResult<Invoice?>(invoice);
        }
    }

    private sealed class TestAppointmentAdapter : IAppointmentAdapter
    {
        private readonly Customer _customer;

        public TestAppointmentAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Appointment?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            var appointment = new Appointment(id, _customer.Id, "Annual Service", DateTime.UtcNow, "Scheduled", "Tech User");
            return Task.FromResult<Appointment?>(appointment);
        }
    }
}

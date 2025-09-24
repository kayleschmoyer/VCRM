// File: Api_CustomerEndpointTests.cs
// Summary: Integration test validating the canonical API surface over stubbed adapters.
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CRMAdapter.Api.Security;
using CRMAdapter.CommonContracts;
using CRMAdapter.CommonDomain;
using CRMAdapter.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace CRMAdapter.Tests.IntegrationTests;

/// <summary>
/// Exercises the hosted API surface by issuing HTTP requests against a test server.
/// </summary>
public sealed class Api_CustomerEndpointTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient? _client;
    private Guid _customerId;

    /// <summary>
    /// Initializes a new instance of the <see cref="Api_CustomerEndpointTests"/> class.
    /// </summary>
    public Api_CustomerEndpointTests()
    {
        _customerId = Guid.NewGuid();
        var customer = CreateCustomer(_customerId);
        var bundle = new AdapterBundle(
            new TestCustomerAdapter(customer),
            new TestVehicleAdapter(customer),
            new TestInvoiceAdapter(customer),
            new TestAppointmentAdapter(customer));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AdapterBundle>();
                services.AddSingleton(bundle);
            });
        });
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(AuthPolicies.Roles.Admin));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the canonical customer endpoint returns the adapter projection.
    /// </summary>
    [Fact]
    public async Task GetCustomerById_ReturnsCanonicalCustomer()
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Test client is not initialized.");
        }

        var response = await _client.GetAsync($"/customers/{_customerId}").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Customer>().ConfigureAwait(false);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(_customerId);
        payload.DisplayName.Should().Be("Ada Lovelace");
        payload.PostalAddress.City.Should().Be("London");
    }

    private static string GenerateToken(string role)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecureSigningKeyForCanonicalAdapter123!"));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: "crm-adapter-api",
            audience: "crm-clients",
            subject: new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
            }),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return handler.WriteToken(token);
    }

    private static Customer CreateCustomer(Guid id)
    {
        var address = new PostalAddress("123 Royal St", null, "London", "London", "SW1A", "GB");
        var vehicleReference = new VehicleReference(Guid.NewGuid(), "1FTSW21R08EB53158");
        return new Customer(
            id,
            "Ada Lovelace",
            "ada.lovelace@example.com",
            "+441234567890",
            address,
            new[] { vehicleReference });
    }

    private sealed class TestCustomerAdapter : ICustomerAdapter
    {
        private readonly Customer _customer;

        public TestCustomerAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Customer?>(id == _customer.Id ? _customer : null);
        }

        public Task<IReadOnlyCollection<Customer>> SearchByNameAsync(string nameQuery, int maxResults, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Customer>>(new[] { _customer });
        }

        public Task<IReadOnlyCollection<Customer>> GetRecentCustomersAsync(int maxResults, CancellationToken cancellationToken = default)
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

        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var reference = _customer.Vehicles.First();
            var vehicle = new Vehicle(reference.Id, reference.Vin, "Ford", "F-150", 2023, 12000, _customer.Id);
            return Task.FromResult<Vehicle?>(id == reference.Id ? vehicle : null);
        }
    }

    private sealed class TestInvoiceAdapter : IInvoiceAdapter
    {
        private readonly Customer _customer;

        public TestInvoiceAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = new Invoice(id, _customer.Id, _customer.Vehicles.First().Id, "INV-1001", DateTime.UtcNow, 199.99m, "Paid", new[]
            {
                new InvoiceLine("Oil Change", 1, 99.99m, 8.25m),
            });
            return Task.FromResult<Invoice?>(invoice);
        }

        public Task<IReadOnlyCollection<Invoice>> GetByCustomerAsync(Guid customerId, int maxResults, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Invoice>>(Array.Empty<Invoice>());
        }
    }

    private sealed class TestAppointmentAdapter : IAppointmentAdapter
    {
        private readonly Customer _customer;

        public TestAppointmentAdapter(Customer customer)
        {
            _customer = customer;
        }

        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var appointment = new Appointment(
                id,
                _customer.Id,
                _customer.Vehicles.First().Id,
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(1),
                "Advisor",
                "Scheduled",
                "Service Bay 1");
            return Task.FromResult<Appointment?>(appointment);
        }

        public Task<IReadOnlyCollection<Appointment>> GetByDateAsync(DateTime date, int maxResults, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Appointment>>(Array.Empty<Appointment>());
        }
    }
}

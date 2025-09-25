// AuditLoggerTests.cs: Unit tests covering normalization and fail-open behavior of the audit logger.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRMAdapter.CommonSecurity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMAdapter.Tests.AuditTests;

public sealed class AuditLoggerTests
{
    [Fact]
    public async Task LogAsync_ShouldNormalizeFieldsBeforeForwarding()
    {
        // Arrange
        var sink = new TestAuditSink();
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var auditLogger = new AuditLogger(new[] { sink }, loggerFactory.CreateLogger<AuditLogger>());

        var input = new AuditEvent(
            CorrelationId: string.Empty,
            UserId: "alice@example.com",
            Role: " Sales ",
            Action: "Customer.View",
            EntityId: "12345",
            Timestamp: default,
            Result: AuditResult.Success,
            Metadata: new Dictionary<string, string> { ["client"] = "ui" });

        // Act
        await auditLogger.LogAsync(input);

        // Assert
        sink.Events.Should().HaveCount(1);
        var recorded = sink.Events.Single();
        recorded.CorrelationId.Should().NotBeNullOrWhiteSpace();
        recorded.CorrelationId.Should().NotBe(input.CorrelationId);
        recorded.UserId.Should().EndWith(".com");
        recorded.UserId.Should().NotContain("alice", StringComparison.OrdinalIgnoreCase);
        recorded.Role.Should().Be("Sales");
        recorded.Timestamp.Offset.Should().Be(TimeSpan.Zero);
        recorded.Metadata.Should().ContainKey("client");
    }

    [Fact]
    public async Task LogAsync_ShouldContinueWhenSinkThrows()
    {
        // Arrange
        var failingSink = new ThrowingAuditSink();
        var capturingSink = new TestAuditSink();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddFilter(_ => true));
        var auditLogger = new AuditLogger(new IAuditSink[] { failingSink, capturingSink }, loggerFactory.CreateLogger<AuditLogger>());

        // Act
        await auditLogger.LogAsync(new AuditEvent("corr", "user", "role", "Action", null, DateTimeOffset.UtcNow, AuditResult.Success));

        // Assert
        capturingSink.Events.Should().HaveCount(1);
    }

    private sealed class ThrowingAuditSink : IAuditSink
    {
        public Task WriteAsync(AuditEvent auditEvent, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }
}

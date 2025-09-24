#nullable enable
using System;
using System.Collections.Generic;
using CRMAdapter.CommonInfrastructure;
using FluentAssertions;
using Xunit;

namespace CRMAdapter.Tests.UnitTests.CommonInfrastructure;

public class StructuredAdapterLoggerTests
{
    [Fact]
    public void Constructor_ThrowsWhenNoSinksProvided()
    {
        Action act = () => new StructuredAdapterLogger(Array.Empty<IAdapterLogSink>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LogInformation_AttachesCorrelationId()
    {
        var sink = new TestSink();
        var logger = new StructuredAdapterLogger(new[] { sink });

        logger.LogInformation("Hello");

        sink.Records.Should().HaveCount(1);
        var record = sink.Records[0];
        record.CorrelationId.Should().NotBeNullOrEmpty();
        record.Context.Should().ContainKey("CorrelationId");
        record.Context["CorrelationId"].Should().Be(record.CorrelationId);
    }

    [Fact]
    public void LogInformation_UsesAmbientCorrelationId()
    {
        var sink = new TestSink();
        var logger = new StructuredAdapterLogger(new[] { sink });

        using (AdapterCorrelationScope.BeginScope("ambient"))
        {
            logger.LogInformation("Hello");
        }

        sink.Records.Should().ContainSingle();
        sink.Records[0].CorrelationId.Should().Be("ambient");
    }

    [Fact]
    public void LogError_RecordsExceptionDetails()
    {
        var sink = new TestSink();
        var logger = new StructuredAdapterLogger(new[] { sink });
        var exception = new InvalidOperationException("boom");

        logger.LogError("Failure", exception);

        sink.Records.Should().ContainSingle();
        var record = sink.Records[0];
        record.Exception.Should().Be(exception);
        record.Context.Should().ContainKey("CorrelationId");
    }

    private sealed class TestSink : IAdapterLogSink
    {
        public List<AdapterLogRecord> Records { get; } = new();

        public void Emit(AdapterLogRecord record)
        {
            Records.Add(record);
        }
    }
}

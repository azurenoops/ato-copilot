using System.Diagnostics.Metrics;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Observability;

namespace Ato.Copilot.Tests.Unit.Observability;

/// <summary>
/// Unit tests for <see cref="ComplianceMetricsService"/>: counter increments,
/// histogram records, and tag validation.
/// </summary>
public class ComplianceMetricsServiceTests
{
    [Fact]
    public void NistApiCalls_IsNotNull()
    {
        ComplianceMetricsService.NistApiCalls.Should().NotBeNull();
    }

    [Fact]
    public void NistApiDuration_IsNotNull()
    {
        ComplianceMetricsService.NistApiDuration.Should().NotBeNull();
    }

    [Fact]
    public void RecordApiCall_Success_DoesNotThrow()
    {
        var act = () => ComplianceMetricsService.RecordApiCall("GetCatalog", success: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordApiCall_Failure_DoesNotThrow()
    {
        var act = () => ComplianceMetricsService.RecordApiCall("GetCatalog", success: false);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordDuration_DoesNotThrow()
    {
        var act = () => ComplianceMetricsService.RecordDuration("GetCatalog", 0.5);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordApiCall_IncrementsCounter()
    {
        // Use MeterListener to verify counter increments
        using var listener = new MeterListener();
        var recorded = false;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "nist_api_calls_total")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "nist_api_calls_total")
                recorded = true;
        });

        listener.Start();
        ComplianceMetricsService.RecordApiCall("SearchControls", success: true);
        listener.RecordObservableInstruments();

        recorded.Should().BeTrue("counter should record a measurement");
    }

    [Fact]
    public void RecordDuration_RecordsHistogram()
    {
        using var listener = new MeterListener();
        var recorded = false;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "nist_api_call_duration_seconds")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "nist_api_call_duration_seconds")
            {
                recorded = true;
                measurement.Should().BeGreaterOrEqualTo(0);
            }
        });

        listener.Start();
        ComplianceMetricsService.RecordDuration("ValidateControlId", 0.05);

        recorded.Should().BeTrue("histogram should record a duration measurement");
    }

    [Fact]
    public void MeterName_IsAtoCopilot()
    {
        ComplianceMetricsService.MeterName.Should().Be("Ato.Copilot");
    }
}

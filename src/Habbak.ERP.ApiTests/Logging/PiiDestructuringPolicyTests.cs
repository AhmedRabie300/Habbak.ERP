using Habbak.ERP.API.Logging;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Settings;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Habbak.ERP.ApiTests.Logging;

/// <summary>Docs/Implementation/HR-Core-Plan.md §0.5 — PII must never reach a log sink, even at Debug.</summary>
public class PiiDestructuringPolicyTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public readonly List<LogEvent> Events = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class EmployeeLogModel
    {
        public string Name { get; set; } = "Ahmed";

        [PiiField(Reason = "National ID")]
        public string NationalId { get; set; } = "29001010112345";
    }

    [Fact]
    public void PiiField_properties_are_redacted_when_the_object_is_destructured()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<PiiDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var model = new EmployeeLogModel();
        logger.Information("Employee touched {@Employee}", model);

        var logEvent = Assert.Single(sink.Events);
        var structure = Assert.IsType<StructureValue>(logEvent.Properties["Employee"]);

        var name = Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == nameof(EmployeeLogModel.Name)).Value);
        Assert.Equal("Ahmed", name.Value);

        var nationalId = Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == nameof(EmployeeLogModel.NationalId)).Value);
        Assert.Equal(AuditLog.Redacted, nationalId.Value);

        var rendered = logEvent.RenderMessage();
        Assert.DoesNotContain(model.NationalId, rendered);
        Assert.Contains(AuditLog.Redacted, rendered);
    }

    [Fact]
    public void Objects_with_no_PiiField_property_are_left_to_the_default_policy()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<PiiDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Plain object {@Plain}", new { Name = "Ahmed" });

        var logEvent = Assert.Single(sink.Events);
        var structure = Assert.IsType<StructureValue>(logEvent.Properties["Plain"]);
        var name = Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == "Name").Value);
        Assert.Equal("Ahmed", name.Value);
    }
}

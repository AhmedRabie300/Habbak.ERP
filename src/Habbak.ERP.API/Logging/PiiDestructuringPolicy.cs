using System.Reflection;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Settings;
using Serilog.Core;
using Serilog.Events;

namespace Habbak.ERP.API.Logging;

/// <summary>
/// Applies whenever a log call forces destructuring (the `@` operator) on an object carrying one or
/// more [PiiField] properties (Domain.Common.PiiFieldAttribute, Docs/Implementation/HR-Core-Plan.md
/// §0.4) — each such property is replaced with AuditLog.Redacted instead of its real value, so PII
/// never reaches a log sink, even at Debug level (00-Project-Overview.md §6.2). Objects with no
/// [PiiField] property are left to Serilog's own default destructuring.
/// </summary>
public sealed class PiiDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToList();

        if (properties.TrueForAll(p => p.GetCustomAttribute<PiiFieldAttribute>() is null))
        {
            result = null!;
            return false;
        }

        var logProperties = properties.Select(p =>
        {
            var isPii = p.GetCustomAttribute<PiiFieldAttribute>() is not null;
            object? propertyValue;
            try
            {
                propertyValue = isPii ? AuditLog.Redacted : p.GetValue(value);
            }
            catch
            {
                propertyValue = null;
            }

            return new LogEventProperty(p.Name, propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: !isPii));
        });

        result = new StructureValue(logProperties, value.GetType().Name);
        return true;
    }
}

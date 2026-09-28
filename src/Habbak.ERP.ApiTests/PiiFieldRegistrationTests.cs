using System.Reflection;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Settings;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Guards Docs/Modules/00-Project-Overview.md §6.2 and 10-Module-HR-Payroll.md rule 6: every Domain
/// property marked [PiiField] must be registered in FieldPermissionCatalog (on some screen), or its
/// value would be logged and audited in the clear instead of redacted as AuditLog.Redacted.
/// </summary>
public class PiiFieldRegistrationTests
{
    private static IEnumerable<(Type Type, PropertyInfo Property)> PiiFieldPropertiesOf(IEnumerable<Type> types) =>
        types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<PiiFieldAttribute>() is not null)
            .Select(p => (Type: t, Property: p)));

    private static void AssertAllAreCataloged(IEnumerable<Type> types)
    {
        var missing = PiiFieldPropertiesOf(types)
            .Where(x => !FieldPermissionCatalog.IsSensitive(x.Type.Name, x.Property.Name))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        Assert.True(missing.Count == 0, $"[PiiField] properties missing from FieldPermissionCatalog: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_PiiField_property_in_Domain_is_registered_in_FieldPermissionCatalog()
    {
        AssertAllAreCataloged(typeof(PiiFieldAttribute).Assembly.GetTypes());
    }

    // A property marked [PiiField] but never listed in FieldPermissionCatalog — proves the check above
    // would actually fail if a real Domain entity forgot to register a PII field, without needing to
    // add and then revert a field on a real entity to demonstrate it.
    private sealed class UnregisteredPiiExample
    {
        [PiiField]
        public string? SomeUnregisteredSecret { get; set; }
    }

    [Fact]
    public void The_check_fails_for_a_PiiField_property_that_is_not_cataloged()
    {
        var ex = Assert.Throws<Xunit.Sdk.TrueException>(() => AssertAllAreCataloged([typeof(UnregisteredPiiExample)]));
        Assert.Contains("UnregisteredPiiExample.SomeUnregisteredSecret", ex.Message);
    }
}

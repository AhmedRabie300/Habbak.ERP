namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Marks a Domain entity property as personally identifiable information — the property must then be
/// registered in <c>FieldPermissionCatalog</c> (any screen), enforced by a reflection test
/// (Docs/Implementation/HR-Core-Plan.md, section 0.4), so it is never logged or audited in the clear.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class PiiFieldAttribute : Attribute
{
    /// <summary>Why the field is PII — optional, for readers of the code, not enforced.</summary>
    public string? Reason { get; set; }
}

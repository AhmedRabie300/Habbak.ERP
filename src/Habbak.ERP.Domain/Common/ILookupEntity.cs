namespace Habbak.ERP.Domain.Common;

/// <summary>
/// The minimum shape every dropdown's data source implements, system-wide
/// (00-System-Wide-Corrections-02.md, section 1/2.1) — every reference/classification entity in
/// the system has its own dedicated table and implements this alongside its own columns, even
/// when it has no extra fields of its own (section 2.2). There is no shared generic lookup table.
/// </summary>
public interface ILookupEntity
{
    long Id { get; set; }
    string Code { get; set; }
    string NameAr { get; set; }
    string NameEn { get; set; }
    bool IsActive { get; set; }
}

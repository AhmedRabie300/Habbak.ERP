namespace Habbak.ERP.Application.Accounting.Accounts.Dtos;

/// <summary>
/// Lightweight lookup shape for account-picker dropdowns (Journal Entry lines, Voucher
/// treasury/direct account) — distinct from AccountTreeNodeDto, used by the full Chart of
/// Accounts screen.
/// </summary>
public sealed class AccountOptionDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string AccountType { get; init; }
    public required string Nature { get; init; }
    public required bool IsPostable { get; init; }
}

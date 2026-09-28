namespace Habbak.ERP.Application.Accounting.Accounts.Dtos;

public class AccountTreeNodeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? ParentId { get; init; }
    public required int Level { get; init; }
    public required string AccountType { get; init; }
    public required string Nature { get; init; }
    public required bool IsPostable { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsSharedAcrossCompanies { get; init; }
}

public sealed class AccountDetailDto : AccountTreeNodeDto
{
    public string? CurrencyCode { get; init; }
    public required string RowVersion { get; init; }
}

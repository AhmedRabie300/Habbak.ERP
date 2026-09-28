namespace Habbak.ERP.Application.Accounting.AccountMappings.Dtos;

/// <summary>One row per role, mapped or not — the screen needs to show the gaps as much as the
/// mappings, since an unmapped role is what will eventually stop a template from posting.</summary>
public sealed class CompanyAccountMappingDto
{
    public required string Role { get; init; }
    public required string ExpectedAccountType { get; init; }
    public long? AccountId { get; init; }
    public string? AccountCode { get; init; }
    public string? AccountNameAr { get; init; }
}

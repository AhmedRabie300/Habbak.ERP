namespace Habbak.ERP.Application.Organization.Banks.Dtos;

public sealed class BankDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public string? SwiftCode { get; init; }
    public string? Address { get; init; }
    public long? CountryId { get; init; }
}

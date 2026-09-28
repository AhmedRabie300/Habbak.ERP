namespace Habbak.ERP.Application.Organization.Currencies.Dtos;

public sealed class CurrencyDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsDefault { get; init; }
}

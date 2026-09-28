namespace Habbak.ERP.Application.Organization.Countries.Dtos;

public sealed class CountryDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public string? IsoCode { get; init; }
}

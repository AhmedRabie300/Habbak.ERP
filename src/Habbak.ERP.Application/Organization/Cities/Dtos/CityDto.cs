namespace Habbak.ERP.Application.Organization.Cities.Dtos;

public sealed class CityDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required long CountryId { get; init; }
}

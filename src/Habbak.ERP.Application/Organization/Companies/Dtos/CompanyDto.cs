namespace Habbak.ERP.Application.Organization.Companies.Dtos;

public sealed class CompanyDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? CommercialRegister { get; init; }
    public string? TaxCard { get; init; }
    public required long BaseCurrencyId { get; init; }
    public required string BaseCurrencyCode { get; init; }
    public required bool IsActive { get; init; }
}

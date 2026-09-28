namespace Habbak.ERP.Application.POS.BlendTypes.Dtos;

public sealed class BlendTypeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal PricePerGram { get; init; }
    public required bool IsActive { get; init; }
}

namespace Habbak.ERP.Application.Accounting.PaymentMethods.Dtos;

public sealed class PaymentMethodDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
}

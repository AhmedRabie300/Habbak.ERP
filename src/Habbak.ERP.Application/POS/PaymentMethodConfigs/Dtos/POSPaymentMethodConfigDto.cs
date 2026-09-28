namespace Habbak.ERP.Application.POS.PaymentMethodConfigs.Dtos;

public sealed class POSPaymentMethodConfigDto
{
    public required long Id { get; init; }
    public required long POSTerminalId { get; init; }
    public required long PaymentMethodId { get; init; }
    public required string PaymentMethodNameAr { get; init; }
    public required string PaymentMethodNameEn { get; init; }
    public required bool IsEnabled { get; init; }
    public required long LinkedTreasuryAccountId { get; init; }
    public required string LinkedTreasuryAccountNameAr { get; init; }
}

namespace Habbak.ERP.Application.POS.Invoices.Dtos;

public sealed class POSInvoiceLineDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal LineTotal { get; init; }
}

public sealed class POSPaymentDto
{
    public required long Id { get; init; }
    public required long PaymentMethodId { get; init; }
    public required string PaymentMethodNameAr { get; init; }
    public required decimal Amount { get; init; }
    public string? CardTransactionReference { get; init; }
    public decimal? AmountTendered { get; init; }
    public decimal? ChangeGiven { get; init; }
    public required decimal CashRoundingAdjustment { get; init; }
    public required string Status { get; init; }
}

public sealed class POSInvoiceDetailDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string InvoiceDate { get; init; }
    public required long CheckId { get; init; }
    public required string CheckCode { get; init; }
    public long? CustomerId { get; init; }
    public required string OrderType { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal ManualDiscountAmount { get; init; }
    public required decimal LoyaltyPointsRedeemed { get; init; }
    public required decimal LoyaltyDiscountAmount { get; init; }
    public required decimal ServiceChargeAmount { get; init; }
    public required decimal TipAmount { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal Total { get; init; }
    public required string Status { get; init; }
    public required string ETAReceiptStatus { get; init; }
    public required IReadOnlyList<POSInvoiceLineDto> Lines { get; init; }
    public required IReadOnlyList<POSPaymentDto> Payments { get; init; }
}

public sealed class POSInvoiceListItemDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string InvoiceDate { get; init; }
    public required string OrderType { get; init; }
    public required decimal Total { get; init; }
    public required string Status { get; init; }
}

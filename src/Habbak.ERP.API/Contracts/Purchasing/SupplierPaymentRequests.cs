namespace Habbak.ERP.API.Contracts.Purchasing;

/// <summary>VoucherType and CounterpartyType are fixed by the controller (Payment/Supplier) —
/// never sent by the client. SupplierId/PurchaseInvoiceId name the underlying Voucher's generic
/// CounterpartyId/RelatedInvoiceId for this screen's own vocabulary.</summary>
public sealed record CreateSupplierPaymentRequest(
    long? BranchId,
    DateOnly VoucherDate,
    long TreasuryAccountId,
    string? Description,
    long SupplierId,
    long? PurchaseInvoiceId,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    decimal BaseCurrencyAmount);

public sealed record UpdateSupplierPaymentRequest(
    string RowVersion,
    long? BranchId,
    DateOnly VoucherDate,
    long TreasuryAccountId,
    string? Description,
    long SupplierId,
    long? PurchaseInvoiceId,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    decimal BaseCurrencyAmount);

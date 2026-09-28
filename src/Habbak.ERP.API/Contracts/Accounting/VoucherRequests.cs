using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.API.Contracts.Accounting;

/// <summary>VoucherType is deliberately absent — the controller (route) decides it, never the client.</summary>
public sealed record CreateVoucherRequest(
    long? BranchId,
    DateOnly VoucherDate,
    long TreasuryAccountId,
    string? Description,
    CounterpartyType CounterpartyType,
    long? CounterpartyId,
    long? DirectAccountId,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    decimal BaseCurrencyAmount,
    long? RelatedInvoiceId);

public sealed record UpdateVoucherRequest(
    string RowVersion,
    long? BranchId,
    DateOnly VoucherDate,
    long TreasuryAccountId,
    string? Description,
    CounterpartyType CounterpartyType,
    long? CounterpartyId,
    long? DirectAccountId,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    decimal BaseCurrencyAmount,
    long? RelatedInvoiceId);

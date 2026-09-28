namespace Habbak.ERP.Application.Purchasing.Suppliers.Dtos;

public sealed class SupplierListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string PaymentTerms { get; init; }
    public required string CurrencyCode { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class SupplierDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public required string PaymentTerms { get; init; }
    public decimal? CreditLimit { get; init; }
    public required string CurrencyCode { get; init; }
    public long? DefaultWarehouseId { get; init; }
    public long? PayableAccountId { get; init; }
    public long? ExpenseAccountId { get; init; }
    public required bool IsActive { get; init; }
}

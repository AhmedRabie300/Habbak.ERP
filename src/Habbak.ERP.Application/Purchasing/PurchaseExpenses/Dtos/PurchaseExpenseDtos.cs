namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Dtos;

public sealed class PurchaseExpenseListItemDto
{
    public required long Id { get; init; }
    public required long PurchaseInvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required string AllocationMethod { get; init; }
}

public sealed class PurchaseExpenseDetailDto
{
    public required long Id { get; init; }
    public required long PurchaseInvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required string AllocationMethod { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }
}

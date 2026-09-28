namespace Habbak.ERP.Application.POS.Returns.Dtos;

public sealed record POSReturnLineInput(long ItemId, decimal Quantity, decimal UnitPrice);

public sealed class POSReturnLineDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
}

public sealed class POSReturnListItemDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required string ReturnDate { get; init; }
    public required string SourceInvoiceNumber { get; init; }
    public required string Reason { get; init; }
    public required decimal Total { get; init; }
    public required string Status { get; init; }
}

public sealed class POSReturnDetailDto
{
    public required long Id { get; init; }
    public required string ReturnNumber { get; init; }
    public required string ReturnDate { get; init; }
    public required long SourceInvoiceId { get; init; }
    public required string SourceInvoiceNumber { get; init; }
    public required string Reason { get; init; }
    public required string Status { get; init; }
    public required decimal Total { get; init; }
    public required IReadOnlyList<POSReturnLineDto> Lines { get; init; }
}

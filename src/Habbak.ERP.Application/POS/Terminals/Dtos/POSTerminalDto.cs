namespace Habbak.ERP.Application.POS.Terminals.Dtos;

public sealed class POSTerminalDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required long BranchId { get; init; }
    public long? DefaultWarehouseId { get; init; }
    public required bool IsActive { get; init; }
}

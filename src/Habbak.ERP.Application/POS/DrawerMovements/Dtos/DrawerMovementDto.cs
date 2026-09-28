namespace Habbak.ERP.Application.POS.DrawerMovements.Dtos;

public sealed class DrawerMovementDto
{
    public required long Id { get; init; }
    public required long ShiftId { get; init; }
    public required string MovementType { get; init; }
    public required decimal Amount { get; init; }
    public string? Reason { get; init; }
    public required string Status { get; init; }
    public long? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

namespace Habbak.ERP.Application.POS.ShiftAssignments.Dtos;

public sealed class ShiftAssignmentDto
{
    public required long Id { get; init; }
    public required long POSTerminalId { get; init; }
    public required string POSTerminalNameAr { get; init; }
    public required string POSTerminalNameEn { get; init; }
    public required long UserId { get; init; }
    public required DateOnly AssignedDate { get; init; }
}

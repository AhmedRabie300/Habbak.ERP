namespace Habbak.ERP.Application.POS.DrawerExpenses.Dtos;

public sealed class DrawerExpenseDto
{
    public required long Id { get; init; }
    public required long ShiftId { get; init; }
    public required long ExpenseAccountId { get; init; }
    public required string ExpenseAccountNameAr { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

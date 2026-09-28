namespace Habbak.ERP.Application.Sales.Customers.Dtos;

public sealed class CustomerListItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string CustomerType { get; init; }
    public required decimal CreditLimit { get; init; }
    public required decimal LoyaltyPointsBalance { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class CustomerDetailDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? BranchId { get; init; }
    public required string CustomerType { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public required decimal CreditLimit { get; init; }
    public required int PaymentTermDays { get; init; }
    public long? ReceivableAccountId { get; init; }
    public long? LoyaltyTierId { get; init; }
    public required decimal LoyaltyPointsBalance { get; init; }
    public required bool IsActive { get; init; }
}

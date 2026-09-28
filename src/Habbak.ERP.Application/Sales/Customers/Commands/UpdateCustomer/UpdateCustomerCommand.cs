using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Application.Settings.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Customers.Commands.UpdateCustomer;

public sealed record UpdateCustomerCommand : IRequest
{
    public required long Id { get; init; }
    public long? BranchId { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required CustomerType CustomerType { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public decimal CreditLimit { get; init; }
    public int PaymentTermDays { get; init; }
    public long? ReceivableAccountId { get; init; }
    public long? LoyaltyTierId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCustomerCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentScreen currentScreen) : IRequestHandler<UpdateCustomerCommand>
{
    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        customer.BranchId = request.BranchId;
        customer.NameAr = request.NameAr;
        customer.NameEn = request.NameEn;
        customer.CustomerType = request.CustomerType;
        var rights = await access.GetCurrentAsync(cancellationToken);
        if (rights.FieldOn(currentScreen.Code, "Customer", "Phone").Edit) customer.Phone = request.Phone;
        if (rights.FieldOn(currentScreen.Code, "Customer", "Email").Edit) customer.Email = request.Email;
        customer.Address = request.Address;
        if (rights.FieldOn(currentScreen.Code, "Customer", "CreditLimit").Edit) customer.CreditLimit = request.CreditLimit;
        customer.PaymentTermDays = request.PaymentTermDays;
        customer.ReceivableAccountId = request.ReceivableAccountId;
        customer.LoyaltyTierId = request.LoyaltyTierId;
        customer.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

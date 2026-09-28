using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Application.Settings.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand : IRequest<long>
{
    public string? Code { get; init; }
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
}

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaymentTermDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateCustomerCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator, IUserAccessService access, ICurrentScreen currentScreen)
    : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("SALES_CUSTOMERS", request.Code, cancellationToken);

        var codeExists = await db.Customers
            .AnyAsync(c => c.CompanyId == currentCompanyContext.CompanyId && c.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("SALES-CUSTOMER-CODE-EXISTS", "يوجد عميل آخر بنفس الكود بالفعل.");
        }

        var rights = await access.GetCurrentAsync(cancellationToken);
        var customer = new Customer
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            CustomerType = request.CustomerType,
            Phone = rights.FieldOn(currentScreen.Code, "Customer", "Phone").Edit ? request.Phone : null,
            Email = rights.FieldOn(currentScreen.Code, "Customer", "Email").Edit ? request.Email : null,
            Address = request.Address,
            CreditLimit = rights.FieldOn(currentScreen.Code, "Customer", "CreditLimit").Edit ? request.CreditLimit : 0,
            PaymentTermDays = request.PaymentTermDays,
            ReceivableAccountId = request.ReceivableAccountId,
            LoyaltyTierId = request.LoyaltyTierId,
            LoyaltyPointsBalance = 0,
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}

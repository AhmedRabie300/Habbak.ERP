using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Application.Settings.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Suppliers.Commands.CreateSupplier;

public sealed record CreateSupplierCommand : IRequest<long>
{
    public string? Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? TaxNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public required SupplierPaymentTerms PaymentTerms { get; init; }
    public decimal? CreditLimit { get; init; }
    public required string CurrencyCode { get; init; }
    public long? DefaultWarehouseId { get; init; }
    public long? PayableAccountId { get; init; }
    public long? ExpenseAccountId { get; init; }
}

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
    }
}

public sealed class CreateSupplierCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator, IUserAccessService access, ICurrentScreen currentScreen)
    : IRequestHandler<CreateSupplierCommand, long>
{
    public async Task<long> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Currencies.AnyAsync(c => c.Code == request.CurrencyCode, cancellationToken))
        {
            throw new NotFoundException("Currency", request.CurrencyCode);
        }

        var code = await codeGenerator.ResolveCodeAsync("PURCHASING_SUPPLIERS", request.Code, cancellationToken);

        var codeExists = await db.Suppliers
            .AnyAsync(s => s.CompanyId == currentCompanyContext.CompanyId && s.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("PUR-SUPPLIER-CODE-EXISTS", "يوجد مورد آخر بنفس الكود بالفعل.");
        }

        var rights = await access.GetCurrentAsync(cancellationToken);
        var supplier = new Supplier
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            TaxNumber = request.TaxNumber,
            Phone = rights.FieldOn(currentScreen.Code, "Supplier", "Phone").Edit ? request.Phone : null,
            Email = rights.FieldOn(currentScreen.Code, "Supplier", "Email").Edit ? request.Email : null,
            Address = request.Address,
            PaymentTerms = request.PaymentTerms,
            CreditLimit = rights.FieldOn(currentScreen.Code, "Supplier", "CreditLimit").Edit ? request.CreditLimit : null,
            CurrencyCode = request.CurrencyCode,
            DefaultWarehouseId = request.DefaultWarehouseId,
            PayableAccountId = request.PayableAccountId,
            ExpenseAccountId = request.ExpenseAccountId,
            IsActive = true
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}

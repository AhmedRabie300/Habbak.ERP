using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Application.Settings.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Suppliers.Commands.UpdateSupplier;

public sealed record UpdateSupplierCommand : IRequest
{
    public required long Id { get; init; }
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
    public required bool IsActive { get; init; }
}

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
    }
}

public sealed class UpdateSupplierCommandHandler(IApplicationDbContext db, IUserAccessService access, ICurrentScreen currentScreen) : IRequestHandler<UpdateSupplierCommand>
{
    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        if (!await db.Currencies.AnyAsync(c => c.Code == request.CurrencyCode, cancellationToken))
        {
            throw new NotFoundException("Currency", request.CurrencyCode);
        }

        supplier.NameAr = request.NameAr;
        supplier.NameEn = request.NameEn;
        supplier.TaxNumber = request.TaxNumber;
        var rights = await access.GetCurrentAsync(cancellationToken);
        if (rights.FieldOn(currentScreen.Code, "Supplier", "Phone").Edit) supplier.Phone = request.Phone;
        if (rights.FieldOn(currentScreen.Code, "Supplier", "Email").Edit) supplier.Email = request.Email;
        supplier.Address = request.Address;
        supplier.PaymentTerms = request.PaymentTerms;
        if (rights.FieldOn(currentScreen.Code, "Supplier", "CreditLimit").Edit) supplier.CreditLimit = request.CreditLimit;
        supplier.CurrencyCode = request.CurrencyCode;
        supplier.DefaultWarehouseId = request.DefaultWarehouseId;
        supplier.PayableAccountId = request.PayableAccountId;
        supplier.ExpenseAccountId = request.ExpenseAccountId;
        supplier.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

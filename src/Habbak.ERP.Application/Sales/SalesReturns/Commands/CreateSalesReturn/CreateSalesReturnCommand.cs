using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Commands.CreateSalesReturn;

/// <summary>Creates a sales return as Draft (screen #9). SourceInvoiceId is an optional reference to
/// the original invoice — when set, it must already be Posted (a return only makes sense against a
/// sale that actually happened).</summary>
public sealed record CreateSalesReturnCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required long WarehouseId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public long? SourceInvoiceId { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<SalesReturnLineInput> Lines { get; init; }
}

public sealed class CreateSalesReturnCommandValidator : AbstractValidator<CreateSalesReturnCommand>
{
    public CreateSalesReturnCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ReturnDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("مرتجع المبيعات يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateSalesReturnCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSalesReturnCommand, long>
{
    public async Task<long> Handle(CreateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", request.WarehouseId);
        }

        if (request.SourceInvoiceId is { } sourceInvoiceId)
        {
            var invoiceStatus = await db.SalesInvoices
                .Where(i => i.Id == sourceInvoiceId)
                .Select(i => (SalesInvoiceStatus?)i.Status)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(SalesInvoice), sourceInvoiceId);

            if (invoiceStatus != SalesInvoiceStatus.Posted)
            {
                throw new BusinessRuleException("SALES-RETURN-INVOICE-NOT-POSTED", "لا يمكن إنشاء مرتجع مبيعات مرتبط بفاتورة لم تُرحَّل بعد.");
            }
        }

        var returnNumber = await codeGenerator.ResolveCodeAsync("SALES_RETURN", null, cancellationToken);

        var salesReturn = new SalesReturn
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            SourceInvoiceId = request.SourceInvoiceId,
            ReturnNumber = returnNumber,
            ReturnDate = request.ReturnDate,
            Reason = request.Reason,
            Status = SalesReturnStatus.Draft
        };

        foreach (var line in SalesReturnLineBuilder.Build(request.Lines))
        {
            salesReturn.Lines.Add(line);
        }

        db.SalesReturns.Add(salesReturn);
        await db.SaveChangesAsync(cancellationToken);

        return salesReturn.Id;
    }
}

using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Commands.UpdateSalesReturn;

/// <summary>Edits a Draft sales return — only a Draft can change. SourceInvoiceId is set once at
/// creation and never edited afterward, same as DeliveryOrder's source linkage.</summary>
public sealed record UpdateSalesReturnCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required long WarehouseId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<SalesReturnLineInput> Lines { get; init; }
}

public sealed class UpdateSalesReturnCommandValidator : AbstractValidator<UpdateSalesReturnCommand>
{
    public UpdateSalesReturnCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdateSalesReturnCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalesReturnCommand>
{
    public async Task Handle(UpdateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var salesReturn = await db.SalesReturns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesReturn), request.Id);

        if (salesReturn.Status != SalesReturnStatus.Draft)
        {
            throw new BusinessRuleException("SALES-RETURN-NOT-DRAFT", "لا يمكن تعديل مرتجع المبيعات إلا وهو في حالة مسودة.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", request.WarehouseId);
        }

        db.Entry(salesReturn).Property(nameof(SalesReturn.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        salesReturn.BranchId = request.BranchId;
        salesReturn.CustomerId = request.CustomerId;
        salesReturn.WarehouseId = request.WarehouseId;
        salesReturn.ReturnDate = request.ReturnDate;
        salesReturn.Reason = request.Reason;

        db.SalesReturnLines.RemoveRange(salesReturn.Lines);
        salesReturn.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in SalesReturnLineBuilder.Build(request.Lines))
        {
            salesReturn.Lines.Add(line);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.UpdatePurchaseReturn;

/// <summary>Edits a Draft purchase return — no posting/rejection state exists to lock against
/// besides Draft itself (section 6, PurchaseReturnStatus has no PendingApproval step).</summary>
public sealed record UpdatePurchaseReturnCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required long SupplierId { get; init; }
    public long? PurchaseInvoiceId { get; init; }
    public required long WarehouseId { get; init; }
    public required PurchaseReturnReason Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseReturnLineInput> Lines { get; init; }
}

public sealed class UpdatePurchaseReturnCommandValidator : AbstractValidator<UpdatePurchaseReturnCommand>
{
    public UpdatePurchaseReturnCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ReturnDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("مردود المشتريات يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.UnitId).GreaterThan(0).When(l => l.UnitId is not null);
        });
    }
}

public sealed class UpdatePurchaseReturnCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePurchaseReturnCommand>
{
    public async Task Handle(UpdatePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await db.PurchaseReturns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseReturn), request.Id);

        if (purchaseReturn.Status != PurchaseReturnStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RETURN-NOT-EDITABLE", "لا يمكن تعديل مردود المشتريات إلا وهو في حالة مسودة.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Inventory.Warehouse), request.WarehouseId);
        }

        db.Entry(purchaseReturn).Property(nameof(PurchaseReturn.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        purchaseReturn.BranchId = request.BranchId;
        purchaseReturn.ReturnDate = request.ReturnDate;
        purchaseReturn.SupplierId = request.SupplierId;
        purchaseReturn.PurchaseInvoiceId = request.PurchaseInvoiceId;
        purchaseReturn.WarehouseId = request.WarehouseId;
        purchaseReturn.Reason = request.Reason;
        purchaseReturn.Notes = request.Notes;

        // Two round trips: replacement lines reuse LineNumber 1, 2, 3... and the unique
        // (PurchaseReturnId, LineNumber) index is checked per-statement.
        // Units are checked before the old lines go, so a refused unit leaves the return as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineUnits = request.Lines.Select(l => units.Resolve(l.ItemId, l.UnitId, PurchaseUnits.NotAllowed)).ToList();

        // A return that names an invoice may only send back what that invoice billed (Remarks4,
        // item 8); its own current lines are excluded so editing one does not count against itself.
        var invoiceLineByIndex = purchaseReturn.PurchaseInvoiceId is { } invoiceId
            ? await PurchaseReturnInvoiceMatcher.ValidateAsync(
                db, invoiceId, purchaseReturn.Id, request.Lines,
                index => ItemUnits.ToBase(request.Lines[index].Quantity, lineUnits[index].Factor), cancellationToken)
            : [];

        db.PurchaseReturnLines.RemoveRange(purchaseReturn.Lines);
        purchaseReturn.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var lineNumber = 1;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            var unit = lineUnits[index];
            purchaseReturn.Lines.Add(new PurchaseReturnLine
            {
                LineNumber = lineNumber++,
                ItemId = line.ItemId,
                PurchaseInvoiceLineId = invoiceLineByIndex.TryGetValue(index, out var invoiceLineId) ? invoiceLineId : null,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BaseQuantity = ItemUnits.ToBase(line.Quantity, unit.Factor),
                BaseUnitCost = ItemUnits.CostPerBase(line.UnitCost, unit.Factor),
                BatchNumber = line.BatchNumber
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

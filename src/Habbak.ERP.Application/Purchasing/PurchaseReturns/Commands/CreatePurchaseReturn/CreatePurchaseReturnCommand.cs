using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;

/// <summary>Creates a purchase return as Draft (screen #7). SupplierId and WarehouseId are always
/// required; PurchaseInvoiceId is an optional reference to the original invoice (rule 2's
/// "علاقة مرجعية").</summary>
public sealed record CreatePurchaseReturnCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required long SupplierId { get; init; }
    public long? PurchaseInvoiceId { get; init; }
    public required long WarehouseId { get; init; }
    public required PurchaseReturnReason Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseReturnLineInput> Lines { get; init; }
}

public sealed class CreatePurchaseReturnCommandValidator : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnCommandValidator()
    {
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

public sealed class CreatePurchaseReturnCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePurchaseReturnCommand, long>
{
    public async Task<long> Handle(CreatePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Inventory.Warehouse), request.WarehouseId);
        }

        if (request.PurchaseInvoiceId is { } purchaseInvoiceId
            && !await db.PurchaseInvoices.AnyAsync(i => i.Id == purchaseInvoiceId, cancellationToken))
        {
            throw new NotFoundException(nameof(PurchaseInvoice), purchaseInvoiceId);
        }

        var returnNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_PURCHASE_RETURN", null, cancellationToken);

        var purchaseReturn = new PurchaseReturn
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            ReturnNumber = returnNumber,
            ReturnDate = request.ReturnDate,
            SupplierId = request.SupplierId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason,
            Status = PurchaseReturnStatus.Draft,
            Notes = request.Notes
        };

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ItemId), cancellationToken);
        var lineUnits = request.Lines.Select(l => units.Resolve(l.ItemId, l.UnitId, PurchaseUnits.NotAllowed)).ToList();

        // A return that names an invoice may only send back what that invoice billed (Remarks4, item 8).
        var invoiceLineByIndex = request.PurchaseInvoiceId is { } invoiceId
            ? await PurchaseReturnInvoiceMatcher.ValidateAsync(
                db, invoiceId, null, request.Lines,
                index => ItemUnits.ToBase(request.Lines[index].Quantity, lineUnits[index].Factor), cancellationToken)
            : [];

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

        db.PurchaseReturns.Add(purchaseReturn);
        await db.SaveChangesAsync(cancellationToken);

        return purchaseReturn.Id;
    }
}

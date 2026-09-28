using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.ProductionOrders.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CompleteProductionOrder;

/// <summary>
/// Screen #19's "إكمال الأمر" button. Requires InProgress (state machine, section 4.5).
///
/// Rule 15: ScalingFactor = ActualQuantity ÷ Recipe.OutputQuantity, applied to every RecipeLine's
/// Quantity to compute what actually gets consumed — not necessarily PlannedQuantity's original plan.
///
/// Creates two WarehouseDocuments directly in Posted status (never Draft — same automatic-document
/// rule as InventoryAdjustment from a count closure): ProductionIssue for the consumed components,
/// ProductionReceipt for the output item, each posted through IStockMovementService so StockBalance
/// reflects both sides atomically with this command's own SaveChangesAsync calls.
///
/// Rule 35: ActualCost = cost of the components actually consumed (ProductionIssue lines, at each
/// component's StandardCost) + the financial value of any actual waste recorded — computed only
/// here, at completion, never during InProgress. The output item's own unit cost absorbed into
/// inventory (ProductionReceipt.UnitCost) is the consumed-components cost only: recorded waste is
/// a period loss tracked separately via WasteRecord, not capitalized into the output's stock value.
///
/// Rule 16's cost-variance entry (ActualCost vs StandardCost) would normally post through
/// IPostingService — deferred here for the same reason WarehouseDocument's own Post command defers
/// it (no inventory/variance account wired into the chart of accounts yet); CostVariance is still
/// returned in the result so a future costVarianceReport can be built directly off ProductionOrder
/// rows without this command needing to change.
/// </summary>
public sealed record CompleteProductionOrderCommand : IRequest<CompleteProductionOrderResult>, IIdempotentRequest
{
    public Guid? IdempotencyKey { get; init; }

    public required long Id { get; init; }
    public required decimal ActualQuantity { get; init; }
    public required DateOnly EndDate { get; init; }
    public decimal? ActualWasteQuantity { get; init; }
    public string? WasteReason { get; init; }
}

public sealed class CompleteProductionOrderCommandValidator : AbstractValidator<CompleteProductionOrderCommand>
{
    public CompleteProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ActualQuantity).GreaterThan(0);
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
        RuleFor(x => x.ActualWasteQuantity).GreaterThanOrEqualTo(0).When(x => x.ActualWasteQuantity.HasValue);
        RuleFor(x => x.WasteReason).NotEmpty().When(x => x.ActualWasteQuantity is > 0)
            .WithMessage("سبب الهالك إلزامي لو الكمية أكبر من صفر.");
    }
}

public sealed class CompleteProductionOrderCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator, IStockMovementService stockMovementService)
    : IRequestHandler<CompleteProductionOrderCommand, CompleteProductionOrderResult>
{
    public async Task<CompleteProductionOrderResult> Handle(CompleteProductionOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionOrder), request.Id);

        if (order.Status != ProductionOrderStatus.InProgress)
        {
            throw new BusinessRuleException("INV-PRODORDER-NOT-INPROGRESS", "لا يمكن إكمال أمر الإنتاج إلا وهو قيد التنفيذ.");
        }

        var recipe = await db.Recipes
            .Include(r => r.Lines).ThenInclude(l => l.ComponentItem)
            .FirstOrDefaultAsync(r => r.Id == order.RecipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), order.RecipeId);

        var scalingFactor = request.ActualQuantity / recipe.OutputQuantity;

        var issueDocument = new WarehouseDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            DocumentType = WarehouseDocumentType.ProductionIssue,
            DocumentNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_PRODUCTION_ISSUE", null, cancellationToken),
            DocumentDate = request.EndDate,
            SourceWarehouseId = order.WarehouseId,
            Status = WarehouseDocumentStatus.Posted
        };

        // The issue is in base units: a recipe line may be in any of its item's units.
        var units = await ItemUnits.LoadAsync(db, recipe.Lines.Select(l => l.ComponentItemId).Append(recipe.OutputItemId), cancellationToken);
        var lineNumber = 1;
        foreach (var line in recipe.Lines)
        {
            issueDocument.Lines.Add(new WarehouseDocumentLine
            {
                LineNumber = lineNumber++,
                ItemId = line.ComponentItemId,
                Quantity = ItemUnits.ToBase(line.Quantity, line.UnitFactor) * scalingFactor,
                UnitId = units.Base(line.ComponentItemId).UnitId,
                UnitFactor = 1,
                UnitCost = line.ComponentItem!.StandardCost ?? 0
            });
        }

        db.WarehouseDocuments.Add(issueDocument);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in issueDocument.Lines)
        {
            await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = order.WarehouseId,
                ItemId = line.ItemId,
                TransactionType = TransactionType.ProductionIssue,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                TransactionDate = request.EndDate,
                SourceDocumentType = "WarehouseDocument",
                SourceDocumentId = issueDocument.Id
            }, cancellationToken);
        }

        var consumedComponentsCost = issueDocument.Lines.Sum(l => l.Quantity * l.UnitCost);

        var outputItem = await db.Items.FirstOrDefaultAsync(i => i.Id == recipe.OutputItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), recipe.OutputItemId);
        var wasteValue = (request.ActualWasteQuantity ?? 0) * (outputItem.StandardCost ?? 0);
        var actualCost = consumedComponentsCost + wasteValue;
        var receiptUnitCost = request.ActualQuantity > 0 ? consumedComponentsCost / request.ActualQuantity : 0;

        var receiptDocument = new WarehouseDocument
        {
            CompanyId = currentCompanyContext.CompanyId,
            DocumentType = WarehouseDocumentType.ProductionReceipt,
            DocumentNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_PRODUCTION_RECEIPT", null, cancellationToken),
            DocumentDate = request.EndDate,
            DestinationWarehouseId = order.WarehouseId,
            Status = WarehouseDocumentStatus.Posted,
            Lines =
            {
                new WarehouseDocumentLine
                {
                    LineNumber = 1, ItemId = recipe.OutputItemId, Quantity = request.ActualQuantity, UnitCost = receiptUnitCost,
                    UnitId = units.Base(recipe.OutputItemId).UnitId, UnitFactor = 1
                }
            }
        };

        db.WarehouseDocuments.Add(receiptDocument);
        await db.SaveChangesAsync(cancellationToken);

        await stockMovementService.ApplyMovementAsync(new StockMovementRequest
        {
            CompanyId = currentCompanyContext.CompanyId,
            WarehouseId = order.WarehouseId,
            ItemId = recipe.OutputItemId,
            TransactionType = TransactionType.ProductionReceipt,
            Quantity = request.ActualQuantity,
            UnitCost = receiptUnitCost,
            TransactionDate = request.EndDate,
            SourceDocumentType = "WarehouseDocument",
            SourceDocumentId = receiptDocument.Id
        }, cancellationToken);

        if (request.ActualWasteQuantity is > 0)
        {
            db.WasteRecords.Add(new WasteRecord
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = order.WarehouseId,
                ItemId = recipe.OutputItemId,
                Quantity = request.ActualWasteQuantity.Value,
                WasteDate = request.EndDate,
                Reason = request.WasteReason!,
                SourceDocumentType = WasteSourceDocumentType.ProductionOrder,
                SourceDocumentId = order.Id
            });
        }

        order.ActualQuantity = request.ActualQuantity;
        order.EndDate = request.EndDate;
        order.ActualCost = actualCost;
        order.Status = ProductionOrderStatus.Completed;
        order.ProductionIssueDocumentId = issueDocument.Id;
        order.ProductionReceiptDocumentId = receiptDocument.Id;

        await db.SaveChangesAsync(cancellationToken);

        return new CompleteProductionOrderResult(
            order.Status.ToString(), actualCost, actualCost - order.StandardCost, issueDocument.Id, receiptDocument.Id);
    }
}

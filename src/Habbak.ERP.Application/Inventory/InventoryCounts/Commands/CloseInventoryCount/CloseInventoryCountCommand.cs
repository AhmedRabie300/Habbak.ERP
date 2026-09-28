using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CloseInventoryCount;

/// <summary>
/// Screen #14, final stage — Settled → Closed (rule 11). Posts a single InventoryAdjustment
/// WarehouseDocument, created directly as Posted (automatic-document rule, section 4.1) for every
/// line with SettlementDecision = Approved AND a non-zero variance; rejected lines are excluded
/// from the financial settlement entirely (rule 11's own wording — they need separate manual
/// follow-up outside the count cycle, not a blocker to closing the rest).
///
/// A positive variance (counted &gt; system) posts as AdjustmentIn; negative posts as AdjustmentOut —
/// resolved per line here rather than fixed once for the whole document, unlike every other
/// WarehouseDocumentType, because a single physical count can find some items over and others
/// short at the same time.
///
/// The differences post as one entry from the INVENTORY_COUNT template when one is active —
/// surplus and shortage valued at what the movements actually booked — and it is linked from both
/// the count and its adjustment document.
/// </summary>
public sealed record CloseInventoryCountCommand(long Id, Guid? IdempotencyKey = null)
    : IRequest<CloseInventoryCountResult>, IIdempotentRequest;

public sealed class CloseInventoryCountCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    ICodeGenerator codeGenerator,
    IStockMovementService stockMovementService,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<CloseInventoryCountCommand, CloseInventoryCountResult>
{
    public async Task<CloseInventoryCountResult> Handle(CloseInventoryCountCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.Settled)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-SETTLED", "لا يمكن إقفال الجرد إلا وهو في حالة تمت التسوية.");
        }

        var settledVarianceLines = count.Lines
            .Where(l => l.SettlementDecision == SettlementDecision.Approved && l.VarianceQuantity is not (null or 0))
            .ToList();

        long? adjustmentDocumentId = null;

        if (settledVarianceLines.Count > 0)
        {
            var adjustmentDocument = new WarehouseDocument
            {
                CompanyId = currentCompanyContext.CompanyId,
                DocumentType = WarehouseDocumentType.InventoryAdjustment,
                DocumentNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_ADJUSTMENT", null, cancellationToken),
                DocumentDate = count.CountDate,
                Status = WarehouseDocumentStatus.Posted,
                Notes = $"إقفال جرد {count.CountNumber}"
            };

            // Variances are in base units, so the adjustment lines are too.
            var units = await ItemUnits.LoadAsync(db, settledVarianceLines.Select(l => l.ItemId), cancellationToken);
            var lineNumber = 1;
            foreach (var line in settledVarianceLines)
            {
                // A count surplus is stock appearing with no purchase behind it, so it is valued at
                // what the rest of that warehouse's stock is worth (rule 41 — never at zero).
                // Shortages are outbound and get their cost from the balance regardless.
                adjustmentDocument.Lines.Add(new WarehouseDocumentLine
                {
                    LineNumber = lineNumber++,
                    ItemId = line.ItemId,
                    Quantity = Math.Abs(line.VarianceQuantity!.Value),
                    UnitId = units.Base(line.ItemId).UnitId,
                    UnitFactor = 1,
                    UnitCost = await stockMovementService.ResolveInboundCostAsync(line.ItemId, count.WarehouseId, cancellationToken)
                });
            }

            db.WarehouseDocuments.Add(adjustmentDocument);
            await db.SaveChangesAsync(cancellationToken);

            var increase = 0m;
            var decrease = 0m;
            foreach (var (line, docLine) in settledVarianceLines.Zip(adjustmentDocument.Lines))
            {
                var isIncrease = line.VarianceQuantity!.Value > 0;
                var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
                {
                    CompanyId = currentCompanyContext.CompanyId,
                    WarehouseId = count.WarehouseId,
                    ItemId = line.ItemId,
                    TransactionType = isIncrease ? TransactionType.AdjustmentIn : TransactionType.AdjustmentOut,
                    Quantity = docLine.Quantity,
                    UnitCost = docLine.UnitCost,
                    TransactionDate = count.CountDate,
                    SourceDocumentType = "WarehouseDocument",
                    SourceDocumentId = adjustmentDocument.Id
                }, cancellationToken);

                if (isIncrease)
                {
                    increase += docLine.Quantity * applied.UnitCost;
                }
                else
                {
                    decrease += docLine.Quantity * applied.UnitCost;
                }
            }

            adjustmentDocumentId = adjustmentDocument.Id;

            var companyId = currentCompanyContext.CompanyId;
            var branchId = await db.Warehouses.Where(w => w.Id == count.WarehouseId).Select(w => w.BranchId).FirstOrDefaultAsync(cancellationToken);
            var entry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = companyId,
                BranchId = branchId,
                ScreenCode = PostingScreenCatalog.InventoryCount,
                SourceModule = SourceModule.Inventory,
                SourceDocumentType = SourceDocumentType.InventoryCount,
                SourceDocumentId = count.Id,
                EntryDate = count.CountDate,
                Description = $"تسوية جرد {count.CountNumber}",
                IdempotencyKey = PostingKeys.For(companyId, "InventoryCount.Close", count.Id),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = branchId,
                    ["WarehouseId"] = count.WarehouseId,
                    [PostingScreenCatalog.HasStockMovementField] = increase + decrease > 0,
                    ["IncreaseAmount"] = Math.Round(increase, 2),
                    ["DecreaseAmount"] = Math.Round(decrease, 2)
                })
            }, cancellationToken);

            count.JournalEntry = entry;
            adjustmentDocument.JournalEntry = entry;
        }

        count.Status = InventoryCountStatus.Closed;

        await db.SaveChangesAsync(cancellationToken);

        return new CloseInventoryCountResult(count.Status.ToString(), adjustmentDocumentId);
    }
}

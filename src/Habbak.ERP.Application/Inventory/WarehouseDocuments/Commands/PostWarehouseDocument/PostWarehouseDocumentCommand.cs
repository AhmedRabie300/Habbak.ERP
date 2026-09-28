using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.PostWarehouseDocument;

/// <summary>
/// Posts a Draft warehouse document: applies every line as a stock movement through
/// IStockMovementService, in the SAME SaveChangesAsync call as the document's own status update
/// (atomicity, rule 3's requirement for the StockTransaction+journal-entry pair). The entry comes
/// from the template of the document type's own screen (إذن إضافة / إذن صرف / أرصدة افتتاحية / تسوية)
/// when one is active.
/// Transfers post none: stock moves between warehouses inside the one inventory account.
///
/// TransactionType mapping: StockIn/StockOut are generic manual movements not already tied to a
/// more specific source (Purchasing's own receipt, a transfer, a production order, a POS sale,
/// or a waste record) — they map to AdjustmentIn/AdjustmentOut, the catch-all pair in
/// TransactionType built exactly for this. TransferOrder/TransferReceipt map to the dedicated
/// TransferOut/TransferIn pair: posting the order removes stock from the source warehouse the
/// moment goods leave custody (rule 30), and posting the receipt adds it to the destination only
/// once actually confirmed received — the gap between the two is exactly the in-transit window
/// CustodyOfficerId is accountable for. OpeningBalance maps to its own dedicated TransactionType
/// (not AdjustmentIn) so an initial-setup entry stays distinguishable from a real manual
/// adjustment in the stock ledger.
/// </summary>
public sealed record PostWarehouseDocumentCommand(long Id, Guid? IdempotencyKey = null)
    : IRequest<PostWarehouseDocumentResult>, IIdempotentRequest;

public sealed record PostWarehouseDocumentResult(string Status);

public sealed class PostWarehouseDocumentCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IStockMovementService stockMovementService,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostWarehouseDocumentCommand, PostWarehouseDocumentResult>
{
    public async Task<PostWarehouseDocumentResult> Handle(PostWarehouseDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.WarehouseDocuments
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseDocument), request.Id);

        if (document.Status != WarehouseDocumentStatus.Draft)
        {
            throw new BusinessRuleException("INV-WHDOC-NOT-DRAFT", "لا يمكن ترحيل مستند إلا وهو في حالة مسودة.");
        }

        if (document.DocumentType == WarehouseDocumentType.TransferReceipt
            && document.RelatedWarehouseDocumentId is { } relatedId)
        {
            // Rule 37: a TransferReceipt can't be posted without a valid, Posted TransferOrder behind it.
            var relatedOrder = await db.WarehouseDocuments.FirstOrDefaultAsync(d => d.Id == relatedId, cancellationToken)
                ?? throw new NotFoundException(nameof(WarehouseDocument), relatedId);

            if (relatedOrder.DocumentType != WarehouseDocumentType.TransferOrder || relatedOrder.Status != WarehouseDocumentStatus.Posted)
            {
                throw new BusinessRuleException("INV-R37-TRANSFER-ORDER-NOT-POSTED", "لا يمكن ترحيل استلام التحويل بدون أمر تحويل مُرحَّل صحيح.");
            }
        }

        var (warehouseId, transactionType) = document.DocumentType switch
        {
            WarehouseDocumentType.StockIn => (document.DestinationWarehouseId!.Value, TransactionType.AdjustmentIn),
            WarehouseDocumentType.OpeningBalance => (document.DestinationWarehouseId!.Value, TransactionType.OpeningBalance),
            WarehouseDocumentType.StockOut => (document.SourceWarehouseId!.Value, TransactionType.AdjustmentOut),
            WarehouseDocumentType.TransferOrder => (document.SourceWarehouseId!.Value, TransactionType.TransferOut),
            WarehouseDocumentType.TransferReceipt => (document.DestinationWarehouseId!.Value, TransactionType.TransferIn),
            WarehouseDocumentType.InventoryAdjustment when document.DestinationWarehouseId is not null =>
                (document.DestinationWarehouseId.Value, TransactionType.AdjustmentIn),
            WarehouseDocumentType.InventoryAdjustment when document.SourceWarehouseId is not null =>
                (document.SourceWarehouseId.Value, TransactionType.AdjustmentOut),
            _ => throw new NotSupportedException($"{document.DocumentType} not yet supported.")
        };

        foreach (var line in document.Lines.OrderBy(l => l.LineNumber))
        {
            var movement = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = warehouseId,
                ItemId = line.ItemId,
                TransactionType = transactionType,
                // The line is in its own unit; stock is kept in the item's base unit.
                Quantity = ItemUnits.ToBase(line.Quantity, line.UnitFactor),
                UnitCost = ItemUnits.CostPerBase(line.UnitCost, line.UnitFactor),
                TransactionDate = document.DocumentDate,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
                SourceDocumentType = "WarehouseDocument",
                SourceDocumentId = document.Id
            }, cancellationToken);

            // Rule 40: an outbound line's cost is decided by the source warehouse's average, not by
            // whatever was guessed when the document was drafted. Writing it back onto the line is
            // what lets the matching TransferReceipt carry the exact same figure across instead of
            // re-deriving one at the destination.
            line.UnitCost = movement.UnitCost * line.UnitFactor;
        }

        document.Status = WarehouseDocumentStatus.Posted;

        if (document.DocumentType is not (WarehouseDocumentType.TransferOrder or WarehouseDocumentType.TransferReceipt))
        {
            var cost = Math.Round(document.Lines.Sum(l => l.Quantity * l.UnitCost), 2);
            var inbound = transactionType is TransactionType.AdjustmentIn or TransactionType.OpeningBalance;
            var companyId = currentCompanyContext.CompanyId;

            document.JournalEntry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = companyId,
                BranchId = document.BranchId,
                ScreenCode = document.DocumentType switch
                {
                    WarehouseDocumentType.StockIn => PostingScreenCatalog.StockIn,
                    WarehouseDocumentType.StockOut => PostingScreenCatalog.StockOut,
                    WarehouseDocumentType.OpeningBalance => PostingScreenCatalog.OpeningBalance,
                    _ => PostingScreenCatalog.InventoryAdjustment
                },
                SourceModule = SourceModule.Inventory,
                SourceDocumentType = SourceDocumentType.WarehouseDocument,
                SourceDocumentId = document.Id,
                EntryDate = document.DocumentDate,
                Description = $"مستند مخزن {document.DocumentNumber}",
                IdempotencyKey = PostingKeys.For(companyId, "WarehouseDocument.Post", document.Id),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = document.BranchId,
                    ["WarehouseId"] = warehouseId,
                    [PostingScreenCatalog.HasStockMovementField] = cost > 0,
                    ["CostAmount"] = cost,
                    ["IncreaseAmount"] = inbound ? cost : 0m,
                    ["DecreaseAmount"] = inbound ? 0m : cost
                })
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new PostWarehouseDocumentResult(document.Status.ToString());
    }
}

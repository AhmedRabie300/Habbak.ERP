using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;

/// <summary>
/// The branch a warehouse document belongs to — what the branch data scope filters it by. It is the
/// branch of the warehouse the document works on: the destination for what comes in (stock-in,
/// opening balance, transfer receipt), the source for what goes out (stock-out, transfer order).
/// Only when that warehouse is company-level (the main warehouse, no branch) does the branch the
/// caller gave count — e.g. a transfer order from the main warehouse raised for a branch request
/// belongs to the requesting branch.
///
/// Before this (Remarks3, items 17/20) the screens never sent a branch, so every document was saved
/// company-level and every branch-restricted user saw every branch's transfers.
/// </summary>
public static class WarehouseDocumentBranch
{
    public static async Task<long?> ResolveAsync(
        IApplicationDbContext db, WarehouseDocumentType type, long? sourceWarehouseId, long? destinationWarehouseId, long? requestedBranchId,
        CancellationToken cancellationToken)
    {
        var warehouseId = type switch
        {
            WarehouseDocumentType.StockIn or WarehouseDocumentType.OpeningBalance or WarehouseDocumentType.TransferReceipt
                or WarehouseDocumentType.ProductionReceipt => destinationWarehouseId,
            WarehouseDocumentType.StockOut or WarehouseDocumentType.TransferOrder or WarehouseDocumentType.ProductionIssue => sourceWarehouseId,
            _ => sourceWarehouseId ?? destinationWarehouseId
        };

        if (warehouseId is null)
        {
            return requestedBranchId;
        }

        var warehouseBranchId = await db.Warehouses.Where(w => w.Id == warehouseId).Select(w => w.BranchId).FirstOrDefaultAsync(cancellationToken);
        return warehouseBranchId ?? requestedBranchId;
    }
}

using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IStockMovementService, hosted here — rather than in Application —
/// because it needs direct EF Core access to share the same change tracker/transaction as the
/// calling command handler, same reasoning as PostingService.
///
/// NOTE on concurrency (rule 2): StockBalance.RowVersion is checked automatically by EF at
/// SaveChangesAsync time for an existing row; a conflicting concurrent update throws
/// DbUpdateConcurrencyException, which this service does not catch or retry — the caller's own
/// transaction fails and the operation must be retried from scratch. A bounded retry loop belongs
/// in the higher-traffic callers (WarehouseDocument posting, POS sale) once they're built; adding
/// one here now, with only the low-concurrency Opening Balances screen as a caller, would be
/// premature.
/// </summary>
public class StockMovementService(AppDbContext db) : IStockMovementService
{
    public async Task<StockTransaction> ApplyMovementAsync(StockMovementRequest request, CancellationToken cancellationToken = default)
    {
        var item = await db.Items.FindAsync([request.ItemId], cancellationToken)
            ?? throw new NotFoundException(nameof(Item), request.ItemId);

        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        // Rule 8: tracked items require a batch number on every movement.
        if (item.IsTracked && string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            throw new BusinessRuleException("INV-R8-BATCH-REQUIRED", "هذا الصنف يتطلب رقم دفعة إلزاميًا على أي حركة.");
        }

        var isInbound = request.TransactionType.IsInbound();

        // Rule 32: default the expiry date from the item's shelf life for a tracked item's inbound
        // movement, but let an explicit value (goods printed a different date) win.
        var expiryDate = request.ExpiryDate;
        if (expiryDate is null && isInbound && item.IsTracked && item.ShelfLifeDays is { } shelfLifeDays)
        {
            expiryDate = request.TransactionDate.AddDays(shelfLifeDays);
        }

        var balance = await db.StockBalances
            .FirstOrDefaultAsync(b => b.ItemId == request.ItemId && b.WarehouseId == request.WarehouseId, cancellationToken);

        if (balance is null)
        {
            balance = new StockBalance
            {
                CompanyId = request.CompanyId,
                ItemId = request.ItemId,
                WarehouseId = request.WarehouseId,
                QuantityOnHand = 0
            };
            db.StockBalances.Add(balance);
        }

        var signedDelta = isInbound ? request.Quantity : -request.Quantity;
        var newQuantityOnHand = balance.QuantityOnHand + signedDelta;

        // Rule 1: no movement may take a balance negative, unless this specific warehouse opts out.
        if (newQuantityOnHand < 0 && !warehouse.AllowNegativeBalance)
        {
            throw new BusinessRuleException("INV-R1-NEGATIVE-BALANCE", "لا يمكن إتمام الحركة: الرصيد سيصبح بالسالب.");
        }

        var unitCost = ResolveUnitCost(request, balance, isInbound);

        // Rule 39: the weighted average is recalculated here, inside the caller's transaction,
        // rather than as a later pass — the quantity and the cost it was bought at must never be
        // able to land separately.
        if (isInbound && request.Quantity > 0)
        {
            var openingQuantity = balance.QuantityOnHand;
            var totalQuantity = openingQuantity + request.Quantity;

            // A balance that was negative (an over-issue on a warehouse that allows it) makes the
            // usual formula meaningless — the incoming cost is the only honest figure left.
            balance.AverageCost = openingQuantity > 0 && totalQuantity > 0
                ? ((openingQuantity * balance.AverageCost) + (request.Quantity * unitCost)) / totalQuantity
                : unitCost;

            balance.LastCostUpdateAtUtc = DateTime.UtcNow;
        }

        balance.QuantityOnHand = newQuantityOnHand;

        var transaction = new StockTransaction
        {
            CompanyId = request.CompanyId,
            WarehouseId = request.WarehouseId,
            ItemId = request.ItemId,
            TransactionType = request.TransactionType,
            Quantity = request.Quantity,
            UnitCost = unitCost,
            TransactionDate = request.TransactionDate,
            ExpiryDate = expiryDate,
            BatchNumber = request.BatchNumber,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId
        };

        db.StockTransactions.Add(transaction);

        return transaction;
    }

    public async Task<decimal> ResolveInboundCostAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default)
    {
        var averageCost = await db.StockBalances
            .Where(b => b.ItemId == itemId && b.WarehouseId == warehouseId)
            .Select(b => (decimal?)b.AverageCost)
            .FirstOrDefaultAsync(cancellationToken);

        if (averageCost is > 0)
        {
            return averageCost.Value;
        }

        var standardCost = await db.Items
            .Where(i => i.Id == itemId)
            .Select(i => i.StandardCost)
            .FirstOrDefaultAsync(cancellationToken);

        if (standardCost is > 0)
        {
            return standardCost.Value;
        }

        throw new BusinessRuleException(
            "INV-R41-UNIT-COST-REQUIRED",
            "لا يمكن تحديد تكلفة الصنف: لا يوجد متوسط تكلفة في هذا المخزن ولا تكلفة معيارية على الصنف.");
    }

    /// <summary>
    /// Rule 41: a movement may never be recorded without a real unit cost — that silent zero is how
    /// cost used to evaporate the first time goods moved between warehouses.
    ///
    /// The two directions get it from opposite places. An inbound movement is the only moment the
    /// system learns what something cost, so the caller must supply it. An outbound movement is not
    /// a new price at all: it draws down stock already valued at this warehouse's average, so the
    /// cost is read from the balance and whatever the caller passed is ignored. That also makes it
    /// impossible for a caller to mis-state COGS.
    /// </summary>
    private static decimal ResolveUnitCost(StockMovementRequest request, StockBalance balance, bool isInbound)
    {
        if (!isInbound)
        {
            return balance.AverageCost;
        }

        if (request.UnitCost <= 0)
        {
            throw new BusinessRuleException(
                "INV-R41-UNIT-COST-REQUIRED",
                "لا يمكن ترحيل حركة استلام بدون تكلفة وحدة صريحة أكبر من صفر.");
        }

        return request.UnitCost;
    }
}

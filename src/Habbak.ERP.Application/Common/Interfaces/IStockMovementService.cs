using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// The central stock-movement engine (02-Module-Inventory-Manufacturing.md, section 2.2) — every
/// module that moves inventory (opening balances now; warehouse documents, POS sales, production
/// orders, inventory-count settlements later) creates its StockTransaction/StockBalance changes
/// only through this interface, never by touching those tables directly.
///
/// Does not call SaveChangesAsync: it adds/modifies tracked entities on the shared DbContext and
/// returns the new transaction, so the calling command handler can make its own additional changes
/// and persist everything in a single transaction, mirroring IPostingService.
/// </summary>
public interface IStockMovementService
{
    /// <summary>
    /// Creates the StockTransaction record and atomically adjusts the item's StockBalance in that
    /// warehouse (creating the balance row on the item's first movement there). Throws
    /// <see cref="Exceptions.BusinessRuleException"/> if the movement would take the balance
    /// negative (rule 1) or the item requires a batch number that wasn't supplied (rule 8).
    /// </summary>
    Task<StockTransaction> ApplyMovementAsync(StockMovementRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cost to book goods back in at when the caller has no price of its own — returns and
    /// count surpluses, where nothing was bought and there is no invoice figure to copy.
    ///
    /// Prefers the warehouse's current weighted average (what the stock already there is worth),
    /// falling back to the item's standard cost, and throwing rather than settling for zero
    /// (rule 41) — booking stock in at no cost is how inventory value quietly disappears.
    /// </summary>
    Task<decimal> ResolveInboundCostAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default);
}

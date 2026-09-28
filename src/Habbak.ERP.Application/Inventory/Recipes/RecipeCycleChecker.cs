using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes;

/// <summary>
/// Rule 13: a recipe's components may themselves be the output of other recipes (multi-level BOMs),
/// but the system must reject any save that would create a circular reference — a recipe using
/// itself as a component, directly or through any chain of intermediate recipes.
///
/// Scope decision: the dependency graph is built from EVERY RecipeLine regardless of the owning
/// Recipe's Status or version — even a Draft or a superseded old version represents a component
/// relationship that could become active again, so excluding them would let a cycle slip through
/// (e.g. approving a Draft later, or rule 31 keeping old versions alive for historical ProductionOrders).
/// </summary>
public static class RecipeCycleChecker
{
    public static async Task EnsureNoCycleAsync(
        IApplicationDbContext db,
        long outputItemId,
        IEnumerable<long> componentItemIds,
        long? excludeRecipeId,
        CancellationToken cancellationToken)
    {
        var edges = await db.RecipeLines
            .Where(l => excludeRecipeId == null || l.RecipeId != excludeRecipeId)
            .Select(l => new { OutputItemId = l.Recipe!.OutputItemId, l.ComponentItemId })
            .ToListAsync(cancellationToken);

        var graph = edges
            .GroupBy(e => e.OutputItemId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ComponentItemId).ToList());

        var visited = new HashSet<long>();
        var queue = new Queue<long>(componentItemIds);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == outputItemId)
            {
                throw new BusinessRuleException(
                    "INV-R13-CIRCULAR-RECIPE",
                    "لا يمكن حفظ الوصفة — أحد المكوّنات يؤدي في النهاية إلى استخدام الصنف الناتج نفسه كمكوّن (مرجع دائري).");
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (graph.TryGetValue(current, out var next))
            {
                foreach (var n in next)
                {
                    queue.Enqueue(n);
                }
            }
        }
    }
}

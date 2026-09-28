using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Common;

/// <summary>A line's unit and how many base units one of it holds (1 for the base unit).</summary>
public sealed record ItemUnit(long UnitId, decimal Factor);

/// <summary>
/// The units a line may be entered in (Remarks3, the "unit" field on the inventory screens): the
/// item's base unit, or one of its ItemUnitConversion units. Anything else is refused. A line
/// without a unit is in the base unit, so callers that never sent one keep working unchanged.
///
/// Lines keep their quantity (and cost) in the chosen unit; whatever moves stock multiplies by the
/// factor stored on the line (<see cref="ToBase"/>).
/// </summary>
public sealed class ItemUnits
{
    private readonly Dictionary<long, long> _baseUnits;
    private readonly Dictionary<(long Item, long Unit), decimal> _conversions;

    private ItemUnits(Dictionary<long, long> baseUnits, Dictionary<(long, long), decimal> conversions)
    {
        _baseUnits = baseUnits;
        _conversions = conversions;
    }

    public static async Task<ItemUnits> LoadAsync(IApplicationDbContext db, IEnumerable<long> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();
        var baseUnits = await db.Items.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.BaseUnitOfMeasureId, cancellationToken);
        var conversions = await db.ItemUnitConversions.Where(c => ids.Contains(c.ItemId))
            .Select(c => new { c.ItemId, c.AlternateUnitOfMeasureId, c.ConversionFactor })
            .ToListAsync(cancellationToken);

        return new ItemUnits(
            baseUnits,
            conversions.GroupBy(c => (c.ItemId, c.AlternateUnitOfMeasureId)).ToDictionary(g => g.Key, g => g.First().ConversionFactor));
    }

    /// <summary>The unit a line of <paramref name="itemId"/> is in — the base unit when none was given.</summary>
    public ItemUnit Resolve(long itemId, long? unitId, string errorCode = "INV-UNIT-NOT-ALLOWED")
    {
        if (!_baseUnits.TryGetValue(itemId, out var baseUnit))
        {
            throw new NotFoundException("Item", itemId);
        }

        if (unitId is null || unitId == baseUnit)
        {
            return new ItemUnit(baseUnit, 1m);
        }

        if (_conversions.TryGetValue((itemId, unitId.Value), out var factor) && factor > 0)
        {
            return new ItemUnit(unitId.Value, factor);
        }

        throw new BusinessRuleException(
            errorCode, "الوحدة المختارة مش من وحدات الصنف — اختار الوحدة الأساسية أو وحدة من وحدات التحويل المعرّفة للصنف.");
    }

    /// <summary>The base unit of the item — for lines the system itself creates.</summary>
    public ItemUnit Base(long itemId) => Resolve(itemId, null);

    /// <summary>A quantity in a line's unit, in base units.</summary>
    public static decimal ToBase(decimal quantity, decimal factor) => quantity * factor;

    /// <summary>A cost per a line's unit, per base unit.</summary>
    public static decimal CostPerBase(decimal unitCost, decimal factor) => factor == 0 ? unitCost : unitCost / factor;
}

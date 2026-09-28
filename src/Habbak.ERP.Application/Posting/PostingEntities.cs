using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// How a cost center value is found for an entity named on a document (design notes 2026-09-18,
/// note 3): the dimension linked to that entity's kind holds one value per record, matched by the
/// record's code — the scheme branches already use (BranchDimensionSync).
///
/// A record that has no value yet (a terminal added after its dimension was set up) gets one
/// mirrored from it on the spot, so a new terminal never blocks a sale. Cashiers are the exception:
/// there is no cashier master record to mirror from, so their values are entered by hand, coded with
/// the user number, and a missing one is reported rather than invented.
/// </summary>
internal static class PostingEntityValueMapper
{
    public static async Task<long?> MapAsync(
        IApplicationDbContext db, CostCenterDimension dimension, long entityId, CancellationToken cancellationToken)
    {
        var entity = await DescribeAsync(db, dimension.LinkedEntityType, entityId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var existing = db.CostCenterDimensionValues.Local
                .FirstOrDefault(v => v.CostCenterDimensionId == dimension.Id && v.Code == entity.Code)
            ?? await db.CostCenterDimensionValues
                .FirstOrDefaultAsync(v => v.CostCenterDimensionId == dimension.Id && v.Code == entity.Code, cancellationToken);

        if (existing is not null)
        {
            return existing.IsActive ? existing.Id : null;
        }

        if (dimension.LinkedEntityType == CostCenterLinkedEntityType.Cashier)
        {
            return null;
        }

        var mirrored = new CostCenterDimensionValue
        {
            CostCenterDimensionId = dimension.Id,
            Code = entity.Code,
            NameAr = entity.NameAr,
            NameEn = entity.NameEn,
            Level = 0,
            IsActive = true
        };
        db.CostCenterDimensionValues.Add(mirrored);

        // The journal line needs a real id; saving here is part of the caller's transaction.
        await db.SaveChangesAsync(cancellationToken);
        return mirrored.Id;
    }

    private sealed record EntityInfo(string Code, string NameAr, string NameEn);

    private static async Task<EntityInfo?> DescribeAsync(
        IApplicationDbContext db, CostCenterLinkedEntityType type, long id, CancellationToken cancellationToken) => type switch
    {
        CostCenterLinkedEntityType.Branch => await db.Branches.Where(e => e.Id == id)
            .Select(e => new EntityInfo(e.Code, e.NameAr, e.NameEn)).FirstOrDefaultAsync(cancellationToken),
        CostCenterLinkedEntityType.POSTerminal => await db.POSTerminals.Where(e => e.Id == id)
            .Select(e => new EntityInfo(e.Code, e.NameAr, e.NameEn)).FirstOrDefaultAsync(cancellationToken),
        CostCenterLinkedEntityType.Warehouse => await db.Warehouses.Where(e => e.Id == id)
            .Select(e => new EntityInfo(e.Code, e.NameAr, e.NameEn)).FirstOrDefaultAsync(cancellationToken),
        CostCenterLinkedEntityType.Customer => await db.Customers.Where(e => e.Id == id)
            .Select(e => new EntityInfo(e.Code, e.NameAr, e.NameEn)).FirstOrDefaultAsync(cancellationToken),
        CostCenterLinkedEntityType.Supplier => await db.Suppliers.Where(e => e.Id == id)
            .Select(e => new EntityInfo(e.Code, e.NameAr, e.NameEn)).FirstOrDefaultAsync(cancellationToken),
        CostCenterLinkedEntityType.Cashier => new EntityInfo(id.ToString(), id.ToString(), id.ToString()),
        _ => null
    };
}

public sealed record RelatedEntityField(string Name, string LabelAr, string LabelEn, CostCenterLinkedEntityType EntityType);

/// <summary>A record a document points to, and the fields of it a cost center can be taken from.</summary>
public sealed record RelatedEntityDefinition(
    string Name, string LabelAr, string LabelEn, string IdField, IReadOnlyList<RelatedEntityField> Fields,
    Func<IApplicationDbContext, long, string, CancellationToken, Task<long?>> Read);

/// <summary>
/// The records "FromRelatedEntity" can reach (design notes, note 3). A closed list in code rather
/// than entity and property names typed into a template: a typed name would only fail on the first
/// real document, and a template must never be able to read an arbitrary table.
/// </summary>
public static class RelatedEntityCatalog
{
    public static readonly IReadOnlyList<RelatedEntityDefinition> All =
    [
        new("Shift", "الوردية", "Shift", "ShiftId",
            [
                new("CashierUserId", "الكاشير", "Cashier", CostCenterLinkedEntityType.Cashier),
                new("POSTerminalId", "نقطة البيع", "Terminal", CostCenterLinkedEntityType.POSTerminal),
                new("BranchId", "الفرع", "Branch", CostCenterLinkedEntityType.Branch)
            ],
            async (db, id, field, ct) => field switch
            {
                "CashierUserId" => await db.Shifts.Where(s => s.Id == id).Select(s => (long?)s.CashierUserId).FirstOrDefaultAsync(ct),
                "POSTerminalId" => await db.Shifts.Where(s => s.Id == id).Select(s => (long?)s.POSTerminalId).FirstOrDefaultAsync(ct),
                "BranchId" => await db.Shifts.Where(s => s.Id == id).Select(s => s.BranchId).FirstOrDefaultAsync(ct),
                _ => null
            }),

        new("POSTerminal", "نقطة البيع", "Terminal", "POSTerminalId",
            [
                new("BranchId", "الفرع", "Branch", CostCenterLinkedEntityType.Branch),
                new("DefaultWarehouseId", "مخزن الجهاز", "Terminal warehouse", CostCenterLinkedEntityType.Warehouse)
            ],
            async (db, id, field, ct) => field switch
            {
                "BranchId" => await db.POSTerminals.Where(t => t.Id == id).Select(t => (long?)t.BranchId).FirstOrDefaultAsync(ct),
                "DefaultWarehouseId" => await db.POSTerminals.Where(t => t.Id == id).Select(t => t.DefaultWarehouseId).FirstOrDefaultAsync(ct),
                _ => null
            }),

        new("Warehouse", "المخزن", "Warehouse", "WarehouseId",
            [new("BranchId", "فرع المخزن", "Warehouse branch", CostCenterLinkedEntityType.Branch)],
            async (db, id, field, ct) => field switch
            {
                "BranchId" => await db.Warehouses.Where(w => w.Id == id).Select(w => w.BranchId).FirstOrDefaultAsync(ct),
                _ => null
            })
    ];

    public static RelatedEntityDefinition? Find(string? name) =>
        All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}

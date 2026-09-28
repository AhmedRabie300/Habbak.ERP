using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks;

/// <summary>يحلّ سعر الصنف من قائمة الأسعار السارية على الفرع حسب نوع الطلب الحالي (قاعدة 10) —
/// PriceListLine بيحمل الأعمدة الثلاثة (DineIn/Takeaway/Delivery) على نفس السطر (04-Module-Sales.md
/// قاعدة 4)، فمفيش داعي لقائمة أسعار منفصلة لكل قناة. لو مفيش قائمة سعرية سارية للفرع أو الصنف
/// مش موجود فيها، الرجوع لـItem.DefaultPrice كحل احتياطي أخير.</summary>
internal static class PriceResolver
{
    public static async Task<decimal?> ResolveUnitPriceAsync(
        IApplicationDbContext db, long branchId, long itemId, CheckOrderType orderType, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var candidatePriceListIds = await db.PriceLists
            .Where(pl => pl.IsActive
                && pl.EffectiveFromDate <= today
                && (pl.EffectiveToDate == null || pl.EffectiveToDate >= today)
                && pl.Branches.Any(b => b.BranchId == branchId))
            .OrderByDescending(pl => pl.EffectiveFromDate)
            .Select(pl => pl.Id)
            .ToListAsync(cancellationToken);

        foreach (var priceListId in candidatePriceListIds)
        {
            var line = await db.PriceListLines
                .FirstOrDefaultAsync(l => l.PriceListId == priceListId && l.ItemId == itemId, cancellationToken);

            if (line is not null)
            {
                return orderType switch
                {
                    CheckOrderType.DineIn => line.DineInPrice,
                    CheckOrderType.Takeaway => line.TakeawayPrice,
                    CheckOrderType.Delivery => line.DeliveryPrice,
                    _ => line.DineInPrice
                };
            }
        }

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        return item?.DefaultPrice;
    }
}

using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.ChangeCheckOrderType;

/// <summary>قاعدة 10: تغيير OrderType على شيك فيه بنود يعيد تسعير كل CheckLine.UnitPrice تلقائيًا
/// من العمود المطابق في نفس PriceListLine — إلا لو السعر كان مُعدَّل يدويًا
/// (IsPriceManuallyOverridden)، ده بيفضل زي ما هو احترامًا لقرار الكاشير الصريح. الانتقال بعيد عن
/// DineIn يفصل الطرابيزة تلقائيًا (تتحول لـCleaning زي أي شيك بينتهي، قاعدة 30) لأن طلب مش-صالة
/// مايصحش يفضل شاغل طرابيزة.</summary>
public sealed record ChangeCheckOrderTypeCommand(long Id, CheckOrderType OrderType) : IRequest;

public sealed class ChangeCheckOrderTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<ChangeCheckOrderTypeCommand>
{
    public async Task Handle(ChangeCheckOrderTypeCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks
            .Include(c => c.Lines)
            .Include(c => c.Table)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.Id);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن تعديل شيك منتهٍ.");
        }

        if (request.OrderType != CheckOrderType.DineIn && check.TableId is not null)
        {
            if (check.Table is { Status: TableStatus.Busy } table)
            {
                table.Status = TableStatus.Cleaning;
            }

            check.TableId = null;
        }

        check.OrderType = request.OrderType;

        foreach (var line in check.Lines)
        {
            if (line.IsPriceManuallyOverridden)
            {
                continue;
            }

            var resolvedPrice = await PriceResolver.ResolveUnitPriceAsync(db, check.BranchId!.Value, line.ItemId, request.OrderType, cancellationToken);
            if (resolvedPrice is { } price)
            {
                line.UnitPrice = price;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

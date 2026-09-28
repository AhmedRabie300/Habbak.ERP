using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.DeliveryPlatformOrders.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DeliveryPlatformOrders.Queries.GetDeliveryOrdersList;

/// <summary>شاشة #10 — تجمع طلبات المنصات الخارجية (`DeliveryPlatformOrder`) مع الدليفري الداخلي
/// (`Check` بنوع `Delivery` مش مرتبط بأي `DeliveryPlatformOrder`) في قائمة واحدة (05-Module-POS-
/// Shifts.md، جدول الشاشات #10).</summary>
public sealed record GetDeliveryOrdersListQuery(long? POSTerminalId) : IRequest<IReadOnlyList<DeliveryOrderListItemDto>>;

public sealed class GetDeliveryOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDeliveryOrdersListQuery, IReadOnlyList<DeliveryOrderListItemDto>>
{
    public async Task<IReadOnlyList<DeliveryOrderListItemDto>> Handle(GetDeliveryOrdersListQuery request, CancellationToken cancellationToken)
    {
        var platformOrdersQuery = db.DeliveryPlatformOrders.AsNoTracking().Include(o => o.Check).ThenInclude(c => c!.Lines)
            .Where(o => o.CheckId != null);

        if (request.POSTerminalId is { } terminalId)
        {
            platformOrdersQuery = platformOrdersQuery.Where(o => o.Check!.POSTerminalId == terminalId);
        }

        var platformOrders = await platformOrdersQuery.ToListAsync(cancellationToken);

        var internalDeliveryQuery = db.Checks.AsNoTracking().Include(c => c.Lines)
            .Where(c => c.OrderType == CheckOrderType.Delivery && !db.DeliveryPlatformOrders.Any(o => o.CheckId == c.Id));

        if (request.POSTerminalId is { } terminalId2)
        {
            internalDeliveryQuery = internalDeliveryQuery.Where(c => c.POSTerminalId == terminalId2);
        }

        var internalDeliveryChecks = await internalDeliveryQuery.ToListAsync(cancellationToken);

        var items = new List<DeliveryOrderListItemDto>();

        items.AddRange(platformOrders.Select(o => new DeliveryOrderListItemDto
        {
            CheckId = o.Check!.Id,
            CheckCode = o.Check.CheckCode,
            Status = o.Check.Status.ToString(),
            IsExternalPlatform = true,
            PlatformName = o.PlatformName,
            PlatformOrderId = o.PlatformOrderId,
            CustomerName = o.CustomerName,
            CustomerPhone = o.CustomerPhone,
            DeliveryAddress = o.DeliveryAddress,
            LineCount = o.Check.Lines.Count,
            Total = o.Check.Lines.Sum(l => l.Quantity * l.UnitPrice - l.DiscountAmount),
            CreatedAtUtc = o.CreatedAtUtc
        }));

        items.AddRange(internalDeliveryChecks.Select(c => new DeliveryOrderListItemDto
        {
            CheckId = c.Id,
            CheckCode = c.CheckCode,
            Status = c.Status.ToString(),
            IsExternalPlatform = false,
            LineCount = c.Lines.Count,
            Total = c.Lines.Sum(l => l.Quantity * l.UnitPrice - l.DiscountAmount),
            CreatedAtUtc = c.CreatedAtUtc
        }));

        return items.OrderByDescending(i => i.CreatedAtUtc).ToList();
    }
}

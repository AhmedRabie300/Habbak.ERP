using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Checks;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DeliveryPlatformOrders.Commands.ReceiveDeliveryPlatformOrder;

public sealed record DeliveryOrderItemInput(long ItemId, decimal Quantity, decimal? UnitPrice);

/// <summary>قاعدة 17: نقطة استقبال طلب من منصة توصيل خارجية (Talabat/Elmenus وغيرها) — تمثّل الـwebhook
/// اللي منصة حقيقية هتنادي عليه، وتُستخدَم حاليًا كإدخال يدوي/محاكاة من الكاشير لعدم توفر تكامل API
/// فعلي. `(PlatformName, PlatformOrderId)` فريدين على مستوى الشركة (فهرس فريد مفلتَر في
/// DeliveryPlatformOrderConfiguration) — إعادة إرسال نفس الطلب (Webhook مكرر من المنصة نفسها) بترجّع
/// نفس `{id, checkId}` المخزَّنين بدل معالجته مرتين. IdempotencyKey (قاعدة 34) إضافي فوق كده لمعاملة
/// مُزامَنة من جهاز Offline بنفس صرامة POSInvoice/Shift.</summary>
public sealed record ReceiveDeliveryPlatformOrderCommand(
    long POSTerminalId,
    string PlatformName,
    string PlatformOrderId,
    string? CustomerName,
    string? CustomerPhone,
    string? DeliveryAddress,
    IReadOnlyList<DeliveryOrderItemInput> Items,
    Guid? IdempotencyKey = null) : IRequest<ReceiveDeliveryPlatformOrderResult>, IIdempotentRequest;

public sealed record ReceiveDeliveryPlatformOrderResult(long Id, long CheckId);

public sealed class ReceiveDeliveryPlatformOrderCommandValidator : AbstractValidator<ReceiveDeliveryPlatformOrderCommand>
{
    public ReceiveDeliveryPlatformOrderCommandValidator()
    {
        RuleFor(x => x.POSTerminalId).GreaterThan(0);
        RuleFor(x => x.PlatformName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PlatformOrderId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty().WithMessage("لا يمكن استقبال طلب دليفري بدون أصناف.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).When(i => i.UnitPrice.HasValue);
        });
    }
}

public sealed class ReceiveDeliveryPlatformOrderCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<ReceiveDeliveryPlatformOrderCommand, ReceiveDeliveryPlatformOrderResult>
{
    public async Task<ReceiveDeliveryPlatformOrderResult> Handle(ReceiveDeliveryPlatformOrderCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == request.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);

        var existing = await db.DeliveryPlatformOrders.FirstOrDefaultAsync(
            o => o.CompanyId == terminal.CompanyId && o.PlatformName == request.PlatformName && o.PlatformOrderId == request.PlatformOrderId,
            cancellationToken);

        if (existing is not null)
        {
            return new ReceiveDeliveryPlatformOrderResult(existing.Id, existing.CheckId!.Value);
        }

        var openShift = await db.Shifts.FirstOrDefaultAsync(
            s => s.POSTerminalId == request.POSTerminalId && s.Status == ShiftStatus.Open, cancellationToken)
            ?? throw new BusinessRuleException("POS-NO-OPEN-SHIFT", "لا توجد وردية مفتوحة حاليًا على هذا الجهاز.");

        var checkCode = await codeGenerator.ResolveCodeAsync("POS_CHECKS", null, cancellationToken);

        var check = new Check
        {
            CompanyId = terminal.CompanyId,
            BranchId = terminal.BranchId,
            POSTerminalId = request.POSTerminalId,
            ShiftId = openShift.Id,
            CheckCode = checkCode,
            TableId = null,
            OrderType = CheckOrderType.Delivery,
            Status = CheckStatus.Open,
            CustomerId = null
        };

        var lineNumber = 1;
        foreach (var item in request.Items)
        {
            if (!await db.Items.AnyAsync(i => i.Id == item.ItemId, cancellationToken))
            {
                throw new NotFoundException("Item", item.ItemId);
            }

            var unitPrice = item.UnitPrice
                ?? await PriceResolver.ResolveUnitPriceAsync(db, terminal.BranchId!.Value, item.ItemId, CheckOrderType.Delivery, cancellationToken)
                ?? throw new BusinessRuleException("POS-CHECK-ITEM-NO-PRICE", "لا يوجد سعر محدد لأحد أصناف الطلب.");

            check.Lines.Add(new CheckLine
            {
                LineNumber = lineNumber++,
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                IsPriceManuallyOverridden = item.UnitPrice.HasValue
            });
        }

        db.Checks.Add(check);
        await db.SaveChangesAsync(cancellationToken);

        var order = new DeliveryPlatformOrder
        {
            CompanyId = terminal.CompanyId,
            BranchId = terminal.BranchId,
            PlatformName = request.PlatformName,
            PlatformOrderId = request.PlatformOrderId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            DeliveryAddress = request.DeliveryAddress,
            CheckId = check.Id
        };

        db.DeliveryPlatformOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        return new ReceiveDeliveryPlatformOrderResult(order.Id, check.Id);
    }
}

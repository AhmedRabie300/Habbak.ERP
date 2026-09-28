using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Commands.CreateDeliveryOrder;

/// <summary>
/// Creates a delivery order as Draft (screen #8). No stock movement here — StockBalance only
/// changes at Post (PostDeliveryOrderCommand), same Draft-then-Post-moves-stock pattern as
/// GoodsReceipt on the Purchasing side.
///
/// Rule 31: exactly one of SourceOrderId/SourceInvoiceId must be set — the former means the cycle
/// started from a confirmed sales order (Full/OrderBased/DeliveryBased/Simplified), the latter
/// means it started from an already-posted invoice that still needs goods physically issued
/// (InvoiceWithIssue).
/// </summary>
public sealed record CreateDeliveryOrderCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required long WarehouseId { get; init; }
    public required DateOnly DeliveryDate { get; init; }
    public long? SourceOrderId { get; init; }
    public long? SourceInvoiceId { get; init; }
    public required IReadOnlyList<DeliveryOrderLineInput> Lines { get; init; }
}

public sealed class CreateDeliveryOrderCommandValidator : AbstractValidator<CreateDeliveryOrderCommand>
{
    public CreateDeliveryOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.DeliveryDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("أمر التسليم يحتاج بند واحد على الأقل.");

        RuleFor(x => x)
            .Must(x => (x.SourceOrderId is null) != (x.SourceInvoiceId is null))
            .WithMessage("أمر التسليم لازم يرتبط بأمر بيع أو فاتورة، وليس الاثنين معًا ولا لا واحد منهما (قاعدة 31).");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class CreateDeliveryOrderCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateDeliveryOrderCommand, long>
{
    public async Task<long> Handle(CreateDeliveryOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", request.WarehouseId);
        }

        if (request.SourceOrderId is { } sourceOrderId)
        {
            var orderStatus = await db.SalesOrders
                .Where(o => o.Id == sourceOrderId)
                .Select(o => (SalesOrderStatus?)o.Status)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(SalesOrder), sourceOrderId);

            if (orderStatus is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered))
            {
                throw new BusinessRuleException("SALES-DELIVERY-ORDER-NOT-CONFIRMED", "لا يمكن إنشاء أمر تسليم مرتبط بأمر بيع لم يُؤكَّد بعد أو تم تسليمه بالكامل.");
            }
        }

        if (request.SourceInvoiceId is { } sourceInvoiceId)
        {
            var invoiceStatus = await db.SalesInvoices
                .Where(i => i.Id == sourceInvoiceId)
                .Select(i => (SalesInvoiceStatus?)i.Status)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(SalesInvoice), sourceInvoiceId);

            if (invoiceStatus != SalesInvoiceStatus.Posted)
            {
                throw new BusinessRuleException("SALES-DELIVERY-INVOICE-NOT-POSTED", "لا يمكن إنشاء أمر تسليم مرتبط بفاتورة لم تُرحَّل بعد.");
            }
        }

        var deliveryNumber = await codeGenerator.ResolveCodeAsync("SALES_DELIVERY_ORDER", null, cancellationToken);

        var deliveryOrder = new DeliveryOrder
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            DeliveryNumber = deliveryNumber,
            DeliveryDate = request.DeliveryDate,
            SourceOrderId = request.SourceOrderId,
            SourceInvoiceId = request.SourceInvoiceId,
            Status = DeliveryOrderStatus.Draft
        };

        foreach (var line in DeliveryOrderLineBuilder.Build(request.Lines))
        {
            deliveryOrder.Lines.Add(line);
        }

        db.DeliveryOrders.Add(deliveryOrder);
        await db.SaveChangesAsync(cancellationToken);

        return deliveryOrder.Id;
    }
}

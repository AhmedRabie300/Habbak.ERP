using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Commands.UpdateDeliveryOrder;

/// <summary>Edits a Draft delivery order — only a Draft can change. Source linkage
/// (SourceOrderId/SourceInvoiceId) is set once at creation and never edited afterward.</summary>
public sealed record UpdateDeliveryOrderCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required long WarehouseId { get; init; }
    public required DateOnly DeliveryDate { get; init; }
    public required IReadOnlyList<DeliveryOrderLineInput> Lines { get; init; }
}

public sealed class UpdateDeliveryOrderCommandValidator : AbstractValidator<UpdateDeliveryOrderCommand>
{
    public UpdateDeliveryOrderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.DeliveryDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("أمر التسليم يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class UpdateDeliveryOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateDeliveryOrderCommand>
{
    public async Task Handle(UpdateDeliveryOrderCommand request, CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliveryOrder), request.Id);

        if (deliveryOrder.Status != DeliveryOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-DELIVERY-ORDER-NOT-DRAFT", "لا يمكن تعديل أمر التسليم إلا وهو في حالة مسودة.");
        }

        if (!await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", request.WarehouseId);
        }

        db.Entry(deliveryOrder).Property(nameof(DeliveryOrder.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        deliveryOrder.BranchId = request.BranchId;
        deliveryOrder.WarehouseId = request.WarehouseId;
        deliveryOrder.DeliveryDate = request.DeliveryDate;

        db.DeliveryOrderLines.RemoveRange(deliveryOrder.Lines);
        deliveryOrder.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in DeliveryOrderLineBuilder.Build(request.Lines))
        {
            deliveryOrder.Lines.Add(line);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

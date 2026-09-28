using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Commands.UpdateSalesOrder;

/// <summary>Edits a Draft sales order — قاعدة 16 (لا يُعدَّل بعد بدء التسليم الجزئي) means Draft is
/// already the only editable state, since PartiallyDelivered is unreachable until DeliveryOrder is
/// built.</summary>
public sealed record UpdateSalesOrderCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly OrderDate { get; init; }
    public required IReadOnlyList<SalesOrderLineInput> Lines { get; init; }
}

public sealed class UpdateSalesOrderCommandValidator : AbstractValidator<UpdateSalesOrderCommand>
{
    public UpdateSalesOrderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.OrderDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("أمر البيع يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateSalesOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalesOrderCommand>
{
    public async Task Handle(UpdateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.Id);

        if (order.Status != SalesOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-ORDER-NOT-DRAFT", "لا يمكن تعديل أمر البيع إلا وهو في حالة مسودة.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        db.Entry(order).Property(nameof(SalesOrder.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        order.BranchId = request.BranchId;
        order.CustomerId = request.CustomerId;
        order.OrderDate = request.OrderDate;

        db.SalesOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var (lines, subtotal) = SalesOrderLineBuilder.Build(request.Lines);
        foreach (var line in lines)
        {
            order.Lines.Add(line);
        }

        order.Subtotal = subtotal;

        await db.SaveChangesAsync(cancellationToken);
    }
}

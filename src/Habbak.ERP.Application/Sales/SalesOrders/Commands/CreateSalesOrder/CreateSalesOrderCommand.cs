using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Commands.CreateSalesOrder;

/// <summary>
/// Creates a sales order as Draft (screen #6).
///
/// Rule SalesCycleSettings.RequiresQuoteBeforeOrder: if the company's configured cycle requires an
/// accepted quote first, this rejects creation without one — the first real enforcement point of
/// this module's configurable cycle (mirrors CreatePurchaseOrderCommand's RequiresPurchaseRequest
/// check exactly).
///
/// Linking an accepted SourceQuoteId auto-copies nothing by itself (the caller sends the lines it
/// wants, typically pre-filled from the quote by the frontend) — this command's own job is to
/// validate the quote is still convertible (Accepted and not past ValidUntil, قاعدة 14) and mark it
/// Converted (قاعدة 15).
/// </summary>
public sealed record CreateSalesOrderCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly OrderDate { get; init; }
    public long? SourceQuoteId { get; init; }
    public required IReadOnlyList<SalesOrderLineInput> Lines { get; init; }
}

public sealed class CreateSalesOrderCommandValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderCommandValidator()
    {
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

public sealed class CreateSalesOrderCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSalesOrderCommand, long>
{
    public async Task<long> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var cycleSettings = await db.SalesCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.RequiresQuoteBeforeOrder == true && request.SourceQuoteId is null)
        {
            throw new BusinessRuleException("SALES-ORDER-QUOTE-REQUIRED", "دورة المبيعات المفعّلة تتطلب عرض سعر مقبول قبل إنشاء أمر البيع.");
        }

        SalesQuote? sourceQuote = null;
        if (request.SourceQuoteId is { } sourceQuoteId)
        {
            sourceQuote = await db.SalesQuotes.FirstOrDefaultAsync(q => q.Id == sourceQuoteId, cancellationToken)
                ?? throw new NotFoundException(nameof(SalesQuote), sourceQuoteId);

            if (sourceQuote.Status != SalesQuoteStatus.Accepted)
            {
                throw new BusinessRuleException("SALES-ORDER-QUOTE-NOT-ACCEPTED", "لا يمكن إنشاء أمر بيع من عرض سعر غير مقبول.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (sourceQuote.ValidUntil < today)
            {
                throw new BusinessRuleException("SALES-ORDER-QUOTE-EXPIRED", "لا يمكن تحويل عرض سعر منتهي الصلاحية لأمر بيع — يلزم عرض سعر جديد.");
            }
        }

        var orderNumber = await codeGenerator.ResolveCodeAsync("SALES_ORDER", null, cancellationToken);
        var (lines, subtotal) = SalesOrderLineBuilder.Build(request.Lines);

        var order = new SalesOrder
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            OrderNumber = orderNumber,
            OrderDate = request.OrderDate,
            SourceQuoteId = request.SourceQuoteId,
            Status = SalesOrderStatus.Draft,
            Subtotal = subtotal
        };

        foreach (var line in lines)
        {
            order.Lines.Add(line);
        }

        db.SalesOrders.Add(order);

        if (sourceQuote is not null)
        {
            sourceQuote.Status = SalesQuoteStatus.Converted;
        }

        await db.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}

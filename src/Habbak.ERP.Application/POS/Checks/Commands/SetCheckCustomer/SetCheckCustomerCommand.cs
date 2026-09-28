using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.SetCheckCustomer;

/// <summary>مراجعة 2026-09-13 — يربط عميل بشيك مفتوح بعد إنشائه (لقاعدة الولاء، ولاحقًا شريحة
/// الأسعار/العروض الخاصة بالعميل). CustomerId = null يفصل العميل ويمسح أي استبدال نقاط قائم (مفيش
/// استبدال نقاط بدون عميل مربوط).</summary>
public sealed record SetCheckCustomerCommand(long CheckId, long? CustomerId) : IRequest;

public sealed class SetCheckCustomerCommandValidator : AbstractValidator<SetCheckCustomerCommand>
{
    public SetCheckCustomerCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
    }
}

public sealed class SetCheckCustomerCommandHandler(IApplicationDbContext db) : IRequestHandler<SetCheckCustomerCommand>
{
    public async Task Handle(SetCheckCustomerCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن تعديل عميل شيك منتهٍ.");
        }

        if (request.CustomerId is { } customerId && !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            throw new NotFoundException(nameof(Customer), customerId);
        }

        check.CustomerId = request.CustomerId;

        if (request.CustomerId is null)
        {
            check.LoyaltyPointsToRedeem = null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

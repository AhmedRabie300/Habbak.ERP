using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.UpdateCheckLine;

/// <summary>مسموح بس لبند لسه ما اتبعتش للمطبخ (SentToKitchenAt == null) — بعد الإرسال، أي تعديل
/// (كمية/سعر) لازم يمر عبر إلغاء البند (بسبب إلزامي، RemoveCheckLineCommand) وإضافة بند جديد بدل
/// تعديل صامت على حركة اتبلّغت للمطبخ بالفعل.</summary>
public sealed record UpdateCheckLineCommand(long CheckId, long LineId, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, bool IsPriceManuallyOverridden, string? Note) : IRequest;

public sealed class UpdateCheckLineCommandValidator : AbstractValidator<UpdateCheckLineCommand>
{
    public UpdateCheckLineCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.LineId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCheckLineCommandHandler(
    IApplicationDbContext db, IUserAccessService access, ICurrentCompanyContext current) : IRequestHandler<UpdateCheckLineCommand>
{
    public async Task Handle(UpdateCheckLineCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن تعديل بند في شيك منتهٍ.");
        }

        var line = await db.CheckLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.CheckId == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(CheckLine), request.LineId);

        if (line.SentToKitchenAt is not null)
        {
            throw new BusinessRuleException("POS-CHECK-LINE-SENT-TO-KITCHEN", "لا يمكن تعديل بند اتبعت للمطبخ بالفعل — ألغِ البند بسبب وأضف بندًا جديدًا.");
        }

        // Changing the price by hand is the EditPrice button.
        var priceEdited = request.UnitPrice != line.UnitPrice || (request.IsPriceManuallyOverridden && !line.IsPriceManuallyOverridden);
        if (priceEdited)
        {
            await ButtonGuard.RequireAsync(access, "POS_TABLE_BOARD", "EditPrice", cancellationToken);
            await ButtonGuard.AuditAsync(db, access, current, "POS_TABLE_BOARD", "EditPrice", nameof(CheckLine), line.Id, cancellationToken);
        }

        line.Quantity = request.Quantity;
        line.UnitPrice = request.UnitPrice;
        line.DiscountAmount = request.DiscountAmount;
        line.IsPriceManuallyOverridden = request.IsPriceManuallyOverridden;
        line.Note = request.Note;

        await db.SaveChangesAsync(cancellationToken);
    }
}

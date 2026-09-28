using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.OpenCheckForTable;

/// <summary>قاعدة 7: الضغط على طرابيزة فيها شيك Open/Held يسترجعه؛ طرابيزة Free يفتح شيكًا جديدًا
/// تلقائيًا ويحوّلها لـBusy. قاعدة 3: أي بيع لازم يرتبط بوردية Open حاليًا على نفس الجهاز.</summary>
public sealed record OpenCheckForTableCommand(long POSTerminalId, long TableId) : IRequest<long>;

public sealed class OpenCheckForTableCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator) : IRequestHandler<OpenCheckForTableCommand, long>
{
    public async Task<long> Handle(OpenCheckForTableCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == request.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);

        var openShift = await db.Shifts.FirstOrDefaultAsync(
            s => s.POSTerminalId == request.POSTerminalId && s.Status == ShiftStatus.Open, cancellationToken)
            ?? throw new BusinessRuleException("POS-NO-OPEN-SHIFT", "لا توجد وردية مفتوحة حاليًا على هذا الجهاز.");

        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        if (table.Status == TableStatus.Busy)
        {
            var existingCheck = await db.Checks.FirstOrDefaultAsync(
                c => c.TableId == request.TableId && (c.Status == CheckStatus.Open || c.Status == CheckStatus.Held),
                cancellationToken);

            if (existingCheck is not null)
            {
                return existingCheck.Id;
            }

            // الطرابيزة Busy بدون شيك مرتبط فعليًا حالة غير متسقة (ما ينفعش تحصل عبر المسار العادي) —
            // نصلّحها تلقائيًا هنا بدل ما نمنع الكاشير من فتح شيك جديد على طرابيزة شكليًا مشغولة.
            table.Status = TableStatus.Free;
        }

        if (table.Status != TableStatus.Free)
        {
            throw new BusinessRuleException("POS-TABLE-NOT-AVAILABLE", "الطرابيزة غير متاحة لفتح شيك جديد حاليًا.");
        }

        var checkCode = await codeGenerator.ResolveCodeAsync("POS_CHECKS", null, cancellationToken);

        var check = new Check
        {
            CompanyId = terminal.CompanyId,
            BranchId = terminal.BranchId,
            POSTerminalId = request.POSTerminalId,
            ShiftId = openShift.Id,
            CheckCode = checkCode,
            TableId = request.TableId,
            OrderType = CheckOrderType.DineIn,
            Status = CheckStatus.Open
        };

        db.Checks.Add(check);
        table.Status = TableStatus.Busy;

        await db.SaveChangesAsync(cancellationToken);

        return check.Id;
    }
}

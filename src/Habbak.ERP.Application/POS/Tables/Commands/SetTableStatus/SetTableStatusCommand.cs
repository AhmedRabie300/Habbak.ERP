using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Commands.SetTableStatus;

/// <summary>قاعدة 30: انتقال يدوي بين Free/Reserved/Cleaning فقط — Busy مُدارة حصريًا عبر فتح/إنهاء
/// شيك (OpenCheckForTableCommand وأوامر إنهاء الشيك)، ومينفعش تتغيَّر يدويًا في الاتجاهين.</summary>
public sealed record SetTableStatusCommand(long Id, TableStatus Status) : IRequest;

public sealed class SetTableStatusCommandHandler(IApplicationDbContext db) : IRequestHandler<SetTableStatusCommand>
{
    public async Task Handle(SetTableStatusCommand request, CancellationToken cancellationToken)
    {
        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.Id);

        if (request.Status == TableStatus.Busy)
        {
            throw new BusinessRuleException("POS-TABLE-STATUS-BUSY-MANUAL", "حالة Busy تُدار تلقائيًا عبر فتح شيك، لا يمكن تعيينها يدويًا.");
        }

        if (table.Status == TableStatus.Busy)
        {
            throw new BusinessRuleException("POS-TABLE-BUSY", "لا يمكن تغيير حالة طرابيزة عليها شيك مفتوح حاليًا — أنهِ الشيك أولًا.");
        }

        table.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);
    }
}

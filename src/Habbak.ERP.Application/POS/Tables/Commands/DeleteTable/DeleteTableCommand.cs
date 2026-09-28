using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Commands.DeleteTable;

public sealed record DeleteTableCommand(long Id) : IRequest;

public sealed class DeleteTableCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteTableCommand>
{
    public async Task Handle(DeleteTableCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Tables.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.Id);

        if (entity.Status == TableStatus.Busy)
        {
            throw new BusinessRuleException("POS-TABLE-BUSY", "لا يمكن حذف طرابيزة عليها شيك مفتوح حاليًا.");
        }

        db.Tables.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

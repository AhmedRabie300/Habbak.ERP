using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.BlendTypes.Commands.DeleteBlendType;

/// <summary>بيحذف صف BlendType بس — الصنف الخلفي بيفضل موجود (Soft-Delete زي أي صنف تاني) لأنه
/// ممكن يكون متسجَّل على تذاكر/فواتير قديمة (QRTicketLine/CheckLine/POSInvoiceLine).</summary>
public sealed record DeleteBlendTypeCommand(long Id) : IRequest;

public sealed class DeleteBlendTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteBlendTypeCommand>
{
    public async Task Handle(DeleteBlendTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.BlendTypes.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BlendType), request.Id);

        db.BlendTypes.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

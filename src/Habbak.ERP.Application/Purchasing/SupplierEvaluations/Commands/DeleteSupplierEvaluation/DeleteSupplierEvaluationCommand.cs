using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.DeleteSupplierEvaluation;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteSupplierEvaluationCommand(long Id) : IRequest;

public sealed class DeleteSupplierEvaluationCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteSupplierEvaluationCommand>
{
    public async Task Handle(DeleteSupplierEvaluationCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.SupplierEvaluations.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierEvaluation), request.Id);

        db.SupplierEvaluations.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

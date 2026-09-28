using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.InsuranceOffices.Commands.DeleteInsuranceOffice;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens.</summary>
public sealed record DeleteInsuranceOfficeCommand(long Id) : IRequest;

public sealed class DeleteInsuranceOfficeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteInsuranceOfficeCommand>
{
    public async Task Handle(DeleteInsuranceOfficeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.InsuranceOffices.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InsuranceOffice), request.Id);

        db.InsuranceOffices.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

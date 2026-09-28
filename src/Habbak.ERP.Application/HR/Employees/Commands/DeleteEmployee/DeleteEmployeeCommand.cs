using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.DeleteEmployee;

/// <summary>
/// My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens. No manual
/// children check: EmploymentContract/EmployeeDocument/EmployeeCertification (Batch B3) all reference
/// Employee with DeleteBehavior.Restrict, so the database itself blocks deleting an Employee that
/// still has any of them — same convention as every other FK-protected soft delete in this codebase.
/// </summary>
public sealed record DeleteEmployeeCommand(long Id) : IRequest;

public sealed class DeleteEmployeeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        db.Employees.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

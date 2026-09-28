using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.CancelSupplierContract;

/// <summary>Active → Cancelled — terminal, ends the contract before its natural EndDate.</summary>
public sealed record CancelSupplierContractCommand(long Id) : IRequest;

public sealed class CancelSupplierContractCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelSupplierContractCommand>
{
    public async Task Handle(CancelSupplierContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.SupplierContracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierContract), request.Id);

        if (contract.Status != SupplierContractStatus.Active)
        {
            throw new BusinessRuleException("PUR-CONTRACT-NOT-CANCELLABLE", "لا يمكن إلغاء عقد المورد إلا وهو نشط.");
        }

        contract.Status = SupplierContractStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}

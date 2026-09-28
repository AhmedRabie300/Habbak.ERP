using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.DeletePOSPaymentMethodConfig;

public sealed record DeletePOSPaymentMethodConfigCommand(long Id) : IRequest;

public sealed class DeletePOSPaymentMethodConfigCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePOSPaymentMethodConfigCommand>
{
    public async Task Handle(DeletePOSPaymentMethodConfigCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.POSPaymentMethodConfigs.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSPaymentMethodConfig), request.Id);

        db.POSPaymentMethodConfigs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

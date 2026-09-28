using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Dimensions.Commands.DeleteAccountDimensionLink;

public sealed record DeleteAccountDimensionLinkCommand(long Id) : IRequest;

public sealed class DeleteAccountDimensionLinkCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteAccountDimensionLinkCommand>
{
    public async Task Handle(DeleteAccountDimensionLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await db.AccountDimensionLinks.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AccountDimensionLink), request.Id);

        db.AccountDimensionLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }
}

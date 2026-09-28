using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.UpdatePOSPaymentMethodConfig;

public sealed record UpdatePOSPaymentMethodConfigCommand(long Id, long LinkedTreasuryAccountId, bool IsEnabled) : IRequest;

public sealed class UpdatePOSPaymentMethodConfigCommandValidator : AbstractValidator<UpdatePOSPaymentMethodConfigCommand>
{
    public UpdatePOSPaymentMethodConfigCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.LinkedTreasuryAccountId).GreaterThan(0);
    }
}

public sealed class UpdatePOSPaymentMethodConfigCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePOSPaymentMethodConfigCommand>
{
    public async Task Handle(UpdatePOSPaymentMethodConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await db.POSPaymentMethodConfigs.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(POSPaymentMethodConfig), request.Id);

        if (!await db.Accounts.AnyAsync(a => a.Id == request.LinkedTreasuryAccountId, cancellationToken))
        {
            throw new NotFoundException("Account", request.LinkedTreasuryAccountId);
        }

        config.LinkedTreasuryAccountId = request.LinkedTreasuryAccountId;
        config.IsEnabled = request.IsEnabled;

        await db.SaveChangesAsync(cancellationToken);
    }
}

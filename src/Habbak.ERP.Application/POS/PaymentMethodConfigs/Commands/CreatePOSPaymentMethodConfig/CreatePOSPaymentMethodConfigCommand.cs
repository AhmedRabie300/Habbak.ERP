using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.CreatePOSPaymentMethodConfig;

/// <summary>قاعدة 25: LinkedTreasuryAccountId إلزامي — مُخزَّن بس، بدون تكامل فعلي مع IPostingService
/// بعد (نفس تأجيل الموديول كله).</summary>
public sealed record CreatePOSPaymentMethodConfigCommand(long POSTerminalId, long PaymentMethodId, long LinkedTreasuryAccountId, bool IsEnabled) : IRequest<long>;

public sealed class CreatePOSPaymentMethodConfigCommandValidator : AbstractValidator<CreatePOSPaymentMethodConfigCommand>
{
    public CreatePOSPaymentMethodConfigCommandValidator()
    {
        RuleFor(x => x.POSTerminalId).GreaterThan(0);
        RuleFor(x => x.PaymentMethodId).GreaterThan(0);
        RuleFor(x => x.LinkedTreasuryAccountId).GreaterThan(0);
    }
}

public sealed class CreatePOSPaymentMethodConfigCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreatePOSPaymentMethodConfigCommand, long>
{
    public async Task<long> Handle(CreatePOSPaymentMethodConfigCommand request, CancellationToken cancellationToken)
    {
        if (!await db.POSTerminals.AnyAsync(t => t.Id == request.POSTerminalId, cancellationToken))
        {
            throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);
        }

        if (!await db.PaymentMethods.AnyAsync(p => p.Id == request.PaymentMethodId, cancellationToken))
        {
            throw new NotFoundException("PaymentMethod", request.PaymentMethodId);
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == request.LinkedTreasuryAccountId, cancellationToken))
        {
            throw new NotFoundException("Account", request.LinkedTreasuryAccountId);
        }

        var exists = await db.POSPaymentMethodConfigs.AnyAsync(
            c => c.POSTerminalId == request.POSTerminalId && c.PaymentMethodId == request.PaymentMethodId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("POS-PAYMENT-METHOD-CONFIG-EXISTS", "طريقة الدفع دي مُعرَّفة بالفعل على هذا الجهاز.");
        }

        var config = new POSPaymentMethodConfig
        {
            POSTerminalId = request.POSTerminalId,
            PaymentMethodId = request.PaymentMethodId,
            LinkedTreasuryAccountId = request.LinkedTreasuryAccountId,
            IsEnabled = request.IsEnabled
        };

        db.POSPaymentMethodConfigs.Add(config);
        await db.SaveChangesAsync(cancellationToken);

        return config.Id;
    }
}

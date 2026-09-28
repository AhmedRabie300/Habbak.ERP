using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.PaymentMethods.Commands.UpdatePaymentMethod;

public sealed record UpdatePaymentMethodCommand(long Id, string NameAr, string NameEn, bool IsActive) : IRequest;

public sealed class UpdatePaymentMethodCommandValidator : AbstractValidator<UpdatePaymentMethodCommand>
{
    public UpdatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdatePaymentMethodCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePaymentMethodCommand>
{
    public async Task Handle(UpdatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var paymentMethod = await db.PaymentMethods.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentMethod), request.Id);

        paymentMethod.NameAr = request.NameAr;
        paymentMethod.NameEn = request.NameEn;
        paymentMethod.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

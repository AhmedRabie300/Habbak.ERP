using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.PaymentMethods.Commands.CreatePaymentMethod;

public sealed record CreatePaymentMethodCommand(string? Code, string NameAr, string NameEn) : IRequest<long>;

public sealed class CreatePaymentMethodCommandValidator : AbstractValidator<CreatePaymentMethodCommand>
{
    public CreatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreatePaymentMethodCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreatePaymentMethodCommand, long>
{
    public async Task<long> Handle(CreatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("ACCOUNTING_PAYMENT_METHODS", request.Code, cancellationToken);

        var codeExists = await db.PaymentMethods
            .AnyAsync(p => p.CompanyId == currentCompanyContext.CompanyId && p.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("PAYMENT-METHOD-CODE-EXISTS", "توجد طريقة دفع أخرى بنفس الكود بالفعل.");
        }

        var paymentMethod = new PaymentMethod
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true
        };

        db.PaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync(cancellationToken);

        return paymentMethod.Id;
    }
}

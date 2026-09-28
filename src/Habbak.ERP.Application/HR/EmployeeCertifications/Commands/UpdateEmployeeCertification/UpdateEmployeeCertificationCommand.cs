using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Commands.UpdateEmployeeCertification;

public sealed record UpdateEmployeeCertificationCommand(
    long Id, string NameAr, string NameEn, string Issuer, DateOnly IssueDate, DateOnly? ExpiryDate, string? CertificateNumber) : IRequest;

public sealed class UpdateEmployeeCertificationCommandValidator : AbstractValidator<UpdateEmployeeCertificationCommand>
{
    public UpdateEmployeeCertificationCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Issuer).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CertificateNumber).MaximumLength(100);
        RuleFor(x => x.ExpiryDate).GreaterThan(x => x.IssueDate).When(x => x.ExpiryDate.HasValue)
            .WithMessage("تاريخ الانتهاء لازم يكون بعد تاريخ الإصدار.");
    }
}

public sealed class UpdateEmployeeCertificationCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateEmployeeCertificationCommand>
{
    public async Task Handle(UpdateEmployeeCertificationCommand request, CancellationToken cancellationToken)
    {
        var certification = await db.EmployeeCertifications.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeeCertification), request.Id);

        certification.NameAr = request.NameAr;
        certification.NameEn = request.NameEn;
        certification.Issuer = request.Issuer;
        certification.IssueDate = request.IssueDate;
        certification.ExpiryDate = request.ExpiryDate;
        certification.CertificateNumber = request.CertificateNumber;

        await db.SaveChangesAsync(cancellationToken);
    }
}

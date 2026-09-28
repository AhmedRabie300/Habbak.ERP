using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmployeeCertifications.Commands.CreateEmployeeCertification;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6. BranchId mirrors the employee's own BranchId, same reasoning as CreateEmploymentContractCommand.</summary>
public sealed record CreateEmployeeCertificationCommand(
    long EmployeeId, string NameAr, string NameEn, string Issuer, DateOnly IssueDate, DateOnly? ExpiryDate, string? CertificateNumber) : IRequest<long>;

public sealed class CreateEmployeeCertificationCommandValidator : AbstractValidator<CreateEmployeeCertificationCommand>
{
    public CreateEmployeeCertificationCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Issuer).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CertificateNumber).MaximumLength(100);
        RuleFor(x => x.ExpiryDate).GreaterThan(x => x.IssueDate).When(x => x.ExpiryDate.HasValue)
            .WithMessage("تاريخ الانتهاء لازم يكون بعد تاريخ الإصدار.");
    }
}

public sealed class CreateEmployeeCertificationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateEmployeeCertificationCommand, long>
{
    public async Task<long> Handle(CreateEmployeeCertificationCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var certification = new EmployeeCertification
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Issuer = request.Issuer,
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            CertificateNumber = request.CertificateNumber
        };

        db.EmployeeCertifications.Add(certification);
        await db.SaveChangesAsync(cancellationToken);

        return certification.Id;
    }
}

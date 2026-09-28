using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.UpdateEmployeePersonalData;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5. Same transparency as CreateEmployeePersonalDataCommand
/// (Batch B2): assigning plaintext to NationalIdEncrypted/BankIbanEncrypted is enough, EF's
/// EncryptedStringConverter handles the encryption on save (and decryption on the read this handler
/// does to load the row). The uniqueness re-check excludes this row's own Id — otherwise re-saving
/// the same employee's own unchanged National ID would collide with itself.
/// </summary>
public sealed record UpdateEmployeePersonalDataCommand(
    long EmployeeId,
    string NationalId,
    string? BankIban,
    string? BankName,
    long? BankId,
    long? NationalityId,
    long? CityId,
    long? MilitaryStatusId,
    long? QualificationTypeId,
    DateOnly BirthDate,
    Gender Gender,
    MaritalStatus MaritalStatus,
    string? Address,
    string? PhoneNumber,
    string? PersonalEmail,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    long? EmergencyContactRelationshipTypeId) : IRequest;

public sealed class UpdateEmployeePersonalDataCommandValidator : AbstractValidator<UpdateEmployeePersonalDataCommand>
{
    public UpdateEmployeePersonalDataCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.NationalId).NotEmpty().Matches(@"^\d{14}$").WithMessage("الرقم القومي لازم يكون 14 رقم.");
        RuleFor(x => x.BankIban).Matches(@"^[A-Z]{2}\d{2}[A-Za-z0-9]{10,30}$").When(x => !string.IsNullOrEmpty(x.BankIban))
            .WithMessage("صيغة الـ IBAN غير صحيحة.");
    }
}

public sealed class UpdateEmployeePersonalDataCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IPiiHasher piiHasher)
    : IRequestHandler<UpdateEmployeePersonalDataCommand>
{
    public async Task Handle(UpdateEmployeePersonalDataCommand request, CancellationToken cancellationToken)
    {
        var personalData = await db.EmployeePersonalDataRows.FirstOrDefaultAsync(
            d => d.EmployeeId == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeePersonalData), request.EmployeeId);

        var candidateHashes = await piiHasher.ComputeHashCandidatesAsync(request.NationalId, cancellationToken);
        var alreadyUsed = await db.EmployeePersonalDataRows.AnyAsync(
            d => d.Id != personalData.Id && d.CompanyId == currentCompanyContext.CompanyId && candidateHashes.Contains(d.NationalIdHash),
            cancellationToken);
        if (alreadyUsed)
        {
            throw new BusinessRuleException("HR-NATIONAL-ID-ALREADY-EXISTS", "هذا الرقم القومي مسجّل لموظف آخر بالفعل.");
        }

        personalData.NationalIdEncrypted = request.NationalId;
        personalData.NationalIdHash = await piiHasher.ComputeHashAsync(request.NationalId, cancellationToken);
        personalData.NationalIdLast4 = Last4(request.NationalId);
        personalData.BankIbanEncrypted = request.BankIban;
        personalData.BankIbanLast4 = request.BankIban is null ? null : Last4(request.BankIban);
        personalData.BankName = request.BankName;
        personalData.BankId = request.BankId;
        personalData.NationalityId = request.NationalityId;
        personalData.CityId = request.CityId;
        personalData.MilitaryStatusId = request.MilitaryStatusId;
        personalData.QualificationTypeId = request.QualificationTypeId;
        personalData.BirthDate = request.BirthDate;
        personalData.Gender = request.Gender;
        personalData.MaritalStatus = request.MaritalStatus;
        personalData.Address = request.Address;
        personalData.PhoneNumber = request.PhoneNumber;
        personalData.PersonalEmail = request.PersonalEmail;
        personalData.EmergencyContactName = request.EmergencyContactName;
        personalData.EmergencyContactPhone = request.EmergencyContactPhone;
        personalData.EmergencyContactRelationshipTypeId = request.EmergencyContactRelationshipTypeId;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Last4(string value) => value.Length <= 4 ? value : value[^4..];
}

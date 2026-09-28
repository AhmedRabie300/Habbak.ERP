using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.CreateEmployeePersonalData;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B2 — first real use of EncryptedStringConverter
/// (0.3) and IPiiHasher (0.6). The handler never touches ISecretProtector directly: assigning the
/// plaintext to NationalIdEncrypted/BankIbanEncrypted is enough — AppDbContext.OnModelCreating wires
/// the conversion, so SaveChangesAsync encrypts transparently. NationalIdHash uses
/// ComputeHashCandidatesAsync (current + previous key) for the uniqueness check, so a value written
/// just before a key rotation still collides correctly with one written just after; the stored hash
/// itself is always the current key's (ComputeHashAsync), matching what new writes should use.
/// </summary>
public sealed record CreateEmployeePersonalDataCommand(
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
    long? EmergencyContactRelationshipTypeId) : IRequest<long>;

public sealed class CreateEmployeePersonalDataCommandValidator : AbstractValidator<CreateEmployeePersonalDataCommand>
{
    public CreateEmployeePersonalDataCommandValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.NationalId).NotEmpty().MinimumLength(4);
    }
}

public sealed class CreateEmployeePersonalDataCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IPiiHasher piiHasher)
    : IRequestHandler<CreateEmployeePersonalDataCommand, long>
{
    public async Task<long> Handle(CreateEmployeePersonalDataCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FindAsync([request.EmployeeId], cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        if (await db.EmployeePersonalDataRows.AnyAsync(d => d.EmployeeId == request.EmployeeId, cancellationToken))
        {
            throw new BusinessRuleException("HR-PERSONAL-DATA-ALREADY-EXISTS", "بيانات الموظف الشخصية موجودة بالفعل.");
        }

        var candidateHashes = await piiHasher.ComputeHashCandidatesAsync(request.NationalId, cancellationToken);
        var alreadyUsed = await db.EmployeePersonalDataRows.AnyAsync(
            d => d.CompanyId == currentCompanyContext.CompanyId && candidateHashes.Contains(d.NationalIdHash), cancellationToken);
        if (alreadyUsed)
        {
            throw new BusinessRuleException("HR-NATIONAL-ID-ALREADY-EXISTS", "هذا الرقم القومي مسجّل لموظف آخر بالفعل.");
        }

        var personalData = new EmployeePersonalData
        {
            CompanyId = currentCompanyContext.CompanyId,
            EmployeeId = request.EmployeeId,
            NationalIdEncrypted = request.NationalId,
            NationalIdHash = await piiHasher.ComputeHashAsync(request.NationalId, cancellationToken),
            NationalIdLast4 = Last4(request.NationalId),
            BankIbanEncrypted = request.BankIban,
            BankIbanLast4 = request.BankIban is null ? null : Last4(request.BankIban),
            BankName = request.BankName,
            BankId = request.BankId,
            NationalityId = request.NationalityId,
            CityId = request.CityId,
            MilitaryStatusId = request.MilitaryStatusId,
            QualificationTypeId = request.QualificationTypeId,
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            MaritalStatus = request.MaritalStatus,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            PersonalEmail = request.PersonalEmail,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
            EmergencyContactRelationshipTypeId = request.EmergencyContactRelationshipTypeId
        };

        db.EmployeePersonalDataRows.Add(personalData);
        await db.SaveChangesAsync(cancellationToken);

        return personalData.Id;
    }

    private static string Last4(string value) => value.Length <= 4 ? value : value[^4..];
}

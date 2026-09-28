using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Commands.RevealPiiField;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1b — the one deliberate path to the plaintext of the two
/// encrypted-at-rest fields. EF's EncryptedStringConverter already decrypts on every normal read (the
/// column's C# value is plaintext the moment a query loads it) — this handler adds nothing beyond
/// that; its real job is the mandatory, permission-gated AuditLog row (403 enforcement itself is the
/// API layer's ScreenButton("HR_EMPLOYEES", "RevealPii"), not this handler's). The audit row never
/// stores the revealed VALUE — only who revealed which field of which row, and when — logging the
/// value back into a less-protected table would defeat the point of encrypting it in the first place.
/// </summary>
public sealed record RevealPiiFieldCommand(string EntityType, long EntityId, string FieldName) : IRequest<string>;

public sealed class RevealPiiFieldCommandValidator : AbstractValidator<RevealPiiFieldCommand>
{
    private static readonly string[] AllowedFields = ["NationalIdEncrypted", "BankIbanEncrypted"];

    public RevealPiiFieldCommandValidator()
    {
        RuleFor(x => x.EntityType).Equal("EmployeePersonalData");
        RuleFor(x => x.EntityId).GreaterThan(0);
        RuleFor(x => x.FieldName).Must(f => AllowedFields.Contains(f))
            .WithMessage("هذا الحقل مش من الحقول المسموح إظهارها.");
    }
}

public sealed class RevealPiiFieldCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<RevealPiiFieldCommand, string>
{
    public async Task<string> Handle(RevealPiiFieldCommand request, CancellationToken cancellationToken)
    {
        var personalData = await db.EmployeePersonalDataRows.FirstOrDefaultAsync(
            d => d.Id == request.EntityId, cancellationToken)
            ?? throw new NotFoundException(nameof(EmployeePersonalData), request.EntityId);

        var value = request.FieldName switch
        {
            "NationalIdEncrypted" => personalData.NationalIdEncrypted,
            "BankIbanEncrypted" => personalData.BankIbanEncrypted
                ?? throw new BusinessRuleException("HR-PII-FIELD-EMPTY", "لا يوجد IBAN مسجّل لهذا الموظف."),
            _ => throw new BusinessRuleException("HR-PII-FIELD-NOT-ALLOWED", "هذا الحقل مش من الحقول المسموح إظهارها.")
        };

        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = currentCompanyContext.CompanyId,
            UserId = currentCompanyContext.UserId,
            ActionType = AuditActionType.View,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            FieldName = request.FieldName,
            OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return value;
    }
}

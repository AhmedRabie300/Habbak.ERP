using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.TimeEntries.Commands;

/// <summary>تسجيل يدوي (HR/مشرف) — بيتقبل فورًا (Status = Accepted)، عكس المقترح من POS.Shift.</summary>
public sealed record CreateTimeEntryCommand(long EmployeeId, TimeEntryType EntryType, DateTime TimestampUtc) : IRequest<long>;

public sealed class CreateTimeEntryCommandValidator : AbstractValidator<CreateTimeEntryCommand>
{
    public CreateTimeEntryCommandValidator() => RuleFor(x => x.EmployeeId).GreaterThan(0);
}

public sealed class CreateTimeEntryCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateTimeEntryCommand, long>
{
    public async Task<long> Handle(CreateTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var entity = new TimeEntry
        {
            CompanyId = employee.CompanyId,
            BranchId = employee.BranchId,
            EmployeeId = request.EmployeeId,
            EntryType = request.EntryType,
            TimestampUtc = request.TimestampUtc,
            Source = TimeEntrySource.Manual,
            Status = TimeEntryStatus.Accepted
        };

        db.TimeEntries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>قاعدة 10 — التصحيح بصف جديد، الأصل مايتلمسش خالص.</summary>
public sealed record CorrectTimeEntryCommand(long OriginalId, TimeEntryType EntryType, DateTime TimestampUtc, string Reason) : IRequest<long>;

public sealed class CorrectTimeEntryCommandValidator : AbstractValidator<CorrectTimeEntryCommand>
{
    public CorrectTimeEntryCommandValidator()
    {
        RuleFor(x => x.OriginalId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("سبب التصحيح إلزامي.");
    }
}

public sealed class CorrectTimeEntryCommandHandler(IApplicationDbContext db) : IRequestHandler<CorrectTimeEntryCommand, long>
{
    public async Task<long> Handle(CorrectTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var original = await db.TimeEntries.FirstOrDefaultAsync(t => t.Id == request.OriginalId, cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), request.OriginalId);

        var correction = new TimeEntry
        {
            CompanyId = original.CompanyId,
            BranchId = original.BranchId,
            EmployeeId = original.EmployeeId,
            EntryType = request.EntryType,
            TimestampUtc = request.TimestampUtc,
            Source = TimeEntrySource.Manual,
            Status = TimeEntryStatus.Accepted,
            IsCorrection = true,
            CorrectsTimeEntryId = original.Id
        };

        db.TimeEntries.Add(correction);
        await db.SaveChangesAsync(cancellationToken);
        return correction.Id;
    }
}

public sealed record AcceptTimeEntryCommand(long Id) : IRequest;

public sealed class AcceptTimeEntryCommandHandler(IApplicationDbContext db) : IRequestHandler<AcceptTimeEntryCommand>
{
    public async Task Handle(AcceptTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.TimeEntries.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), request.Id);

        if (entity.Status != TimeEntryStatus.Suggested)
        {
            throw new BusinessRuleException("HR-TIME-ENTRY-NOT-SUGGESTED", "التسجيل ده مش في حالة اقتراح.");
        }

        entity.Status = TimeEntryStatus.Accepted;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DismissTimeEntryCommand(long Id) : IRequest;

public sealed class DismissTimeEntryCommandHandler(IApplicationDbContext db) : IRequestHandler<DismissTimeEntryCommand>
{
    public async Task Handle(DismissTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.TimeEntries.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), request.Id);

        if (entity.Status != TimeEntryStatus.Suggested)
        {
            throw new BusinessRuleException("HR-TIME-ENTRY-NOT-SUGGESTED", "التسجيل ده مش في حالة اقتراح.");
        }

        entity.Status = TimeEntryStatus.Dismissed;
        await db.SaveChangesAsync(cancellationToken);
    }
}

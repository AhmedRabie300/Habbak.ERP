using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveTypes.Commands;

public sealed record CreateLeaveTypeCommand(
    string? Code, string NameAr, string NameEn, LeaveAccrualMethod AccrualMethod, int AnnualDays, int MaxCarryOver,
    bool IsPaid, decimal PaidPercentage, long? DeductFromLeaveTypeId, bool RequiresDocument, int? MaxDaysPerRequest,
    Gender? GenderRestriction, int? MaxTimesInService, bool IsCashableOnTermination) : IRequest<long>;

public sealed class CreateLeaveTypeCommandValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AnnualDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxCarryOver).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidPercentage).InclusiveBetween(0, 100);
    }
}

public sealed class CreateLeaveTypeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateLeaveTypeCommand, long>
{
    public async Task<long> Handle(CreateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        if (request.DeductFromLeaveTypeId is not null &&
            !await db.LeaveTypes.AnyAsync(l => l.Id == request.DeductFromLeaveTypeId && l.CompanyId == current.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(LeaveType), request.DeductFromLeaveTypeId.Value);
        }

        var code = await codeGenerator.ResolveCodeAsync("HR_LEAVE_TYPES", request.Code, cancellationToken);
        if (await db.LeaveTypes.AnyAsync(l => l.CompanyId == current.CompanyId && l.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("HR-LEAVE-TYPE-CODE-EXISTS", "يوجد نوع إجازة آخر بنفس الكود بالفعل.");
        }

        var entity = new LeaveType
        {
            CompanyId = current.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true,
            AccrualMethod = request.AccrualMethod,
            AnnualDays = request.AnnualDays,
            MaxCarryOver = request.MaxCarryOver,
            IsPaid = request.IsPaid,
            PaidPercentage = request.PaidPercentage,
            DeductFromLeaveTypeId = request.DeductFromLeaveTypeId,
            RequiresDocument = request.RequiresDocument,
            MaxDaysPerRequest = request.MaxDaysPerRequest,
            GenderRestriction = request.GenderRestriction,
            MaxTimesInService = request.MaxTimesInService,
            IsCashableOnTermination = request.IsCashableOnTermination
        };

        db.LeaveTypes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateLeaveTypeCommand(
    long Id, string NameAr, string NameEn, LeaveAccrualMethod AccrualMethod, int AnnualDays, int MaxCarryOver,
    bool IsPaid, decimal PaidPercentage, long? DeductFromLeaveTypeId, bool RequiresDocument, int? MaxDaysPerRequest,
    Gender? GenderRestriction, int? MaxTimesInService, bool IsCashableOnTermination, bool IsActive) : IRequest;

public sealed class UpdateLeaveTypeCommandValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    public UpdateLeaveTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaidPercentage).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateLeaveTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateLeaveTypeCommand>
{
    public async Task Handle(UpdateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.LeaveTypes.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveType), request.Id);

        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.AccrualMethod = request.AccrualMethod;
        entity.AnnualDays = request.AnnualDays;
        entity.MaxCarryOver = request.MaxCarryOver;
        entity.IsPaid = request.IsPaid;
        entity.PaidPercentage = request.PaidPercentage;
        entity.DeductFromLeaveTypeId = request.DeductFromLeaveTypeId;
        entity.RequiresDocument = request.RequiresDocument;
        entity.MaxDaysPerRequest = request.MaxDaysPerRequest;
        entity.GenderRestriction = request.GenderRestriction;
        entity.MaxTimesInService = request.MaxTimesInService;
        entity.IsCashableOnTermination = request.IsCashableOnTermination;
        entity.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteLeaveTypeCommand(long Id) : IRequest;

public sealed class DeleteLeaveTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteLeaveTypeCommand>
{
    public async Task Handle(DeleteLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.LeaveTypes.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveType), request.Id);

        db.LeaveTypes.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

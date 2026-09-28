using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — screen PAY_TIPS_DISTRIBUTION,
// approval chain §4.8 row 13 (مدير الفرع → مالية). Draft→Pending→Approved here; Included is set by
// PayrollCalculationService (Sub-Batch 4.4) when a Regular run for the same PayrollPeriodId pulls an
// Approved distribution's lines into PayrollLine rows — never set directly by this command set.

public sealed record TipsDistributionLineDto(long Id, long EmployeeId, decimal Share, decimal Amount);
public sealed record TipsDistributionDto(
    long Id, long? BranchId, long PayrollPeriodId, decimal TotalAmount, TipsDistributionMethod Method, TipsDistributionStatus Status,
    IReadOnlyList<TipsDistributionLineDto> Lines);

public sealed record GetTipsDistributionsQuery : IRequest<IReadOnlyList<TipsDistributionDto>>;

public sealed class GetTipsDistributionsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTipsDistributionsQuery, IReadOnlyList<TipsDistributionDto>>
{
    public async Task<IReadOnlyList<TipsDistributionDto>> Handle(GetTipsDistributionsQuery request, CancellationToken cancellationToken) =>
        await db.TipsDistributions.AsNoTracking().Include(t => t.Lines).OrderByDescending(t => t.Id)
            .Select(t => new TipsDistributionDto(t.Id, t.BranchId, t.PayrollPeriodId, t.TotalAmount, t.Method, t.Status,
                t.Lines.Select(l => new TipsDistributionLineDto(l.Id, l.EmployeeId, l.Share, l.Amount)).ToList()))
            .ToListAsync(cancellationToken);
}

public sealed record GetTipsDistributionByIdQuery(long Id) : IRequest<TipsDistributionDto>;

public sealed class GetTipsDistributionByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTipsDistributionByIdQuery, TipsDistributionDto>
{
    public async Task<TipsDistributionDto> Handle(GetTipsDistributionByIdQuery request, CancellationToken cancellationToken) =>
        await db.TipsDistributions.AsNoTracking().Include(t => t.Lines).Where(t => t.Id == request.Id)
            .Select(t => new TipsDistributionDto(t.Id, t.BranchId, t.PayrollPeriodId, t.TotalAmount, t.Method, t.Status,
                t.Lines.Select(l => new TipsDistributionLineDto(l.Id, l.EmployeeId, l.Share, l.Amount)).ToList()))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(TipsDistribution), request.Id);
}

/// <summary><paramref name="Weights"/> keyed by EmployeeId: ignored for Equal (split evenly), the
/// days/hours/points count for ByDays/ByHours/ByPoints — the caller (frontend, or a future report)
/// computes what those numbers actually are; this command only knows how to turn a weight map into a
/// proportional split, the same arithmetic regardless of what the weight represents.</summary>
public sealed record CreateTipsDistributionCommand(
    long? BranchId, long PayrollPeriodId, decimal TotalAmount, TipsDistributionMethod Method, IReadOnlyDictionary<long, decimal> Weights) : IRequest<long>;

public sealed class CreateTipsDistributionCommandValidator : AbstractValidator<CreateTipsDistributionCommand>
{
    public CreateTipsDistributionCommandValidator()
    {
        RuleFor(x => x.PayrollPeriodId).GreaterThan(0);
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.Weights).NotEmpty();
    }
}

public sealed class CreateTipsDistributionCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreateTipsDistributionCommand, long>
{
    public async Task<long> Handle(CreateTipsDistributionCommand request, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == request.PayrollPeriodId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollPeriod), request.PayrollPeriodId);

        var employeeIds = request.Weights.Keys.ToList();
        var validEmployeeCount = await db.Employees.CountAsync(e => employeeIds.Contains(e.Id), cancellationToken);
        if (validEmployeeCount != employeeIds.Count)
        {
            throw new BusinessRuleException("PAY-TIPS-EMPLOYEE-NOT-FOUND", "فيه موظف في القائمة مش موجود.");
        }

        var weights = request.Method == TipsDistributionMethod.Equal
            ? employeeIds.ToDictionary(id => id, _ => 1m)
            : request.Weights;
        var totalWeight = weights.Values.Sum();
        if (totalWeight <= 0)
        {
            throw new BusinessRuleException("PAY-TIPS-WEIGHTS-ZERO", "مجموع الأوزان (الأيام/الساعات/النقاط) لازم يكون أكبر من صفر.");
        }

        var lines = new List<TipsDistributionLine>();
        decimal allocated = 0;
        var ordered = weights.ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var (employeeId, weight) = ordered[i];
            var isLast = i == ordered.Count - 1;
            var share = weight / totalWeight;
            // The last line absorbs the rounding remainder so lines always sum to exactly TotalAmount.
            var amount = isLast ? request.TotalAmount - allocated : Math.Round(request.TotalAmount * share, 2);
            allocated += amount;
            lines.Add(new TipsDistributionLine { EmployeeId = employeeId, Share = share, Amount = amount });
        }

        var entity = new TipsDistribution
        {
            CompanyId = current.CompanyId, BranchId = request.BranchId, PayrollPeriodId = period.Id,
            TotalAmount = request.TotalAmount, Method = request.Method, Status = TipsDistributionStatus.Draft, Lines = lines
        };
        db.TipsDistributions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>Draft → Pending [§4.8 row 13: مدير الفرع → مالية] → Approved directly when no workflow is
/// assigned yet (rule 4), same EntityType-is-Screen.Code pattern as every other Phase 3/4 submission.</summary>
public sealed record SubmitTipsDistributionCommand(long Id) : IRequest;

public sealed class SubmitTipsDistributionCommandValidator : AbstractValidator<SubmitTipsDistributionCommand>
{
    public SubmitTipsDistributionCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class SubmitTipsDistributionCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IApprovalWorkflowService approvalWorkflowService)
    : IRequestHandler<SubmitTipsDistributionCommand>
{
    public async Task Handle(SubmitTipsDistributionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.TipsDistributions.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TipsDistribution), request.Id);

        if (entity.Status != TipsDistributionStatus.Draft)
        {
            throw new BusinessRuleException("PAY-TIPS-NOT-DRAFT", "توزيع البقشيش ده مش في حالة مسودة.");
        }

        var instanceId = await approvalWorkflowService.TryStartApprovalAsync(new ApprovalWorkflowTrigger
        {
            CompanyId = current.CompanyId,
            EntityType = "PAY_TIPS_DISTRIBUTION",
            EntityId = entity.Id,
            Amount = entity.TotalAmount,
            RequestedByUserId = current.UserId
        }, cancellationToken);

        if (instanceId is not null)
        {
            entity.Status = TipsDistributionStatus.Pending;
            entity.ApprovalInstanceId = instanceId;
        }
        else
        {
            entity.Status = TipsDistributionStatus.Approved;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

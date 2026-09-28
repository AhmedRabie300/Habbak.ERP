using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Settings.Commands.UpdateHrSettings;

/// <summary>Screen HR_SETTINGS — upserts the single HrSettings row for the current company.
/// Phase 1 + Phase 3 (LeaveDayCountingMode) + Phase 4.6 (MonthBasis/DefaultCutoffDay/
/// CompanyDefaultApproverUserId) fields are settable here; KioskSessionSeconds/
/// SelfServiceClockInRequiresLocation/MaxAdvanceInstallmentPercent keep whatever value they
/// already have (entity default on first insert) — still their own phases' screens to open.</summary>
public sealed record UpdateHrSettingsCommand : IRequest
{
    public required int DefaultProbationDays { get; init; }
    public long? DefaultBranchId { get; init; }
    public required bool RequireNationalIdForActivation { get; init; }
    public required LeaveDayCountingMode LeaveDayCountingMode { get; init; }
    public HrMonthBasis? MonthBasis { get; init; }
    public int? DefaultCutoffDay { get; init; }
    public long? CompanyDefaultApproverUserId { get; init; }
}

public sealed class UpdateHrSettingsCommandValidator : AbstractValidator<UpdateHrSettingsCommand>
{
    public UpdateHrSettingsCommandValidator()
    {
        RuleFor(x => x.DefaultProbationDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultCutoffDay).InclusiveBetween(1, 31).When(x => x.DefaultCutoffDay is not null);
    }
}

public sealed class UpdateHrSettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateHrSettingsCommand>
{
    public async Task Handle(UpdateHrSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.DefaultBranchId is not null)
        {
            var branchBelongsToCompany = await db.Branches.AnyAsync(
                b => b.Id == request.DefaultBranchId && b.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
            if (!branchBelongsToCompany)
            {
                throw new NotFoundException(nameof(Branch), request.DefaultBranchId.Value);
            }
        }

        if (request.CompanyDefaultApproverUserId is not null
            && !await db.Users.AnyAsync(u => u.Id == request.CompanyDefaultApproverUserId, cancellationToken))
        {
            throw new NotFoundException(nameof(Habbak.ERP.Domain.Settings.User), request.CompanyDefaultApproverUserId.Value);
        }

        var settings = await db.HrSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (settings is null)
        {
            settings = new HrSettings { CompanyId = currentCompanyContext.CompanyId };
            db.HrSettingsRows.Add(settings);
        }

        settings.DefaultProbationDays = request.DefaultProbationDays;
        settings.DefaultBranchId = request.DefaultBranchId;
        settings.RequireNationalIdForActivation = request.RequireNationalIdForActivation;
        settings.LeaveDayCountingMode = request.LeaveDayCountingMode;
        settings.MonthBasis = request.MonthBasis;
        settings.DefaultCutoffDay = request.DefaultCutoffDay;
        settings.CompanyDefaultApproverUserId = request.CompanyDefaultApproverUserId;

        await db.SaveChangesAsync(cancellationToken);
    }
}

using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Settings.Commands.UpdateHrSettings;

/// <summary>Screen HR_SETTINGS — upserts the single HrSettings row for the current company.
/// Only the Phase-1 fields are settable here; the deferred fields keep whatever value they
/// already have (entity default on first insert).</summary>
public sealed record UpdateHrSettingsCommand : IRequest
{
    public required int DefaultProbationDays { get; init; }
    public long? DefaultBranchId { get; init; }
    public required bool RequireNationalIdForActivation { get; init; }
    public required LeaveDayCountingMode LeaveDayCountingMode { get; init; }
}

public sealed class UpdateHrSettingsCommandValidator : AbstractValidator<UpdateHrSettingsCommand>
{
    public UpdateHrSettingsCommandValidator()
    {
        RuleFor(x => x.DefaultProbationDays).GreaterThanOrEqualTo(0);
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

        await db.SaveChangesAsync(cancellationToken);
    }
}

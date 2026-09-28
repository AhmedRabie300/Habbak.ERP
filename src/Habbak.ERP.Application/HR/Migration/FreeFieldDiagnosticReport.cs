using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Migration;

/// <summary>
/// Phase 1.2 (HR-MASTER-PLAN.md §Phase 1.2, sub-batch 1.2.2) — the free-text technician/officer fields
/// that predate the HR module (<c>CustodyOfficer.EmployeeId</c>, <c>MaintenanceRequest.TechnicianId</c>,
/// <c>MaintenanceSchedule.TechnicianId</c>) have a real employee name typed in already but no FK. This
/// file reports, per record, whether that name matches exactly one active employee ("certain match") or
/// needs a human to decide — and separately applies only the certain matches. It never guesses: an exact
/// match on either NameAr or NameEn, unique among active employees in the same company, or nothing.
/// </summary>
public enum FreeFieldMatchStatus
{
    /// <summary>Already has an EmployeeId/TechnicianId — nothing left to do.</summary>
    AlreadyLinked,

    /// <summary>Exactly one active employee (same company) matches the stored name — safe to auto-link.</summary>
    ExactMatch,

    /// <summary>More than one active employee matches the same name — needs a human to pick.</summary>
    Ambiguous,

    /// <summary>No active employee matches — needs a human, or the employee doesn't exist yet.</summary>
    NoMatch
}

public sealed record FreeFieldCandidateDto(long RecordId, string Code, string FreeTextName, long? MatchedEmployeeId, string? MatchedEmployeeName, FreeFieldMatchStatus MatchStatus);

public sealed record FreeFieldDiagnosticReportDto(
    IReadOnlyList<FreeFieldCandidateDto> CustodyOfficers,
    IReadOnlyList<FreeFieldCandidateDto> MaintenanceRequests,
    IReadOnlyList<FreeFieldCandidateDto> MaintenanceSchedules);

internal sealed record EmployeeNameLookup(long Id, long? CompanyId, string NameAr, string NameEn);

public sealed record GetFreeFieldDiagnosticReportQuery : IRequest<FreeFieldDiagnosticReportDto>;

public sealed class GetFreeFieldDiagnosticReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetFreeFieldDiagnosticReportQuery, FreeFieldDiagnosticReportDto>
{
    public async Task<FreeFieldDiagnosticReportDto> Handle(GetFreeFieldDiagnosticReportQuery request, CancellationToken cancellationToken)
    {
        var employees = await LoadActiveEmployees(db, cancellationToken);

        var officers = await db.CustodyOfficers.AsNoTracking()
            .Select(o => new { o.Id, o.Code, o.CompanyId, o.NameAr, o.NameEn, o.EmployeeId })
            .ToListAsync(cancellationToken);
        var officerCandidates = officers
            .Select(o => Evaluate(o.Id, o.Code, o.NameAr, o.NameEn, o.CompanyId, o.EmployeeId, employees))
            .ToList();

        var requests = await db.MaintenanceRequests.AsNoTracking()
            .Where(r => r.TechnicianName != null)
            .Select(r => new { r.Id, r.RequestNumber, r.CompanyId, r.TechnicianName, r.TechnicianId })
            .ToListAsync(cancellationToken);
        var requestCandidates = requests
            .Select(r => Evaluate(r.Id, r.RequestNumber, r.TechnicianName!, r.TechnicianName!, r.CompanyId, r.TechnicianId, employees))
            .ToList();

        var schedules = await db.MaintenanceSchedules.AsNoTracking()
            .Where(s => s.TechnicianName != null)
            .Select(s => new { s.Id, s.CompanyId, s.TechnicianName, s.TechnicianId })
            .ToListAsync(cancellationToken);
        var scheduleCandidates = schedules
            .Select(s => Evaluate(s.Id, s.Id.ToString(), s.TechnicianName!, s.TechnicianName!, s.CompanyId, s.TechnicianId, employees))
            .ToList();

        return new FreeFieldDiagnosticReportDto(officerCandidates, requestCandidates, scheduleCandidates);
    }

    internal static async Task<List<EmployeeNameLookup>> LoadActiveEmployees(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await db.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new EmployeeNameLookup(e.Id, e.CompanyId, e.NameAr, e.NameEn))
            .ToListAsync(cancellationToken);

    private static FreeFieldCandidateDto Evaluate(
        long recordId, string code, string nameAr, string nameEn, long? companyId, long? currentEmployeeId,
        IReadOnlyList<EmployeeNameLookup> employees)
    {
        if (currentEmployeeId is not null)
        {
            return new FreeFieldCandidateDto(recordId, code, nameAr, currentEmployeeId, null, FreeFieldMatchStatus.AlreadyLinked);
        }

        var matches = FreeFieldNameMatcher.FindMatches(nameAr, nameEn, companyId, employees);

        return matches.Count switch
        {
            1 => new FreeFieldCandidateDto(recordId, code, nameAr, matches[0].Id, matches[0].NameAr, FreeFieldMatchStatus.ExactMatch),
            > 1 => new FreeFieldCandidateDto(recordId, code, nameAr, null, null, FreeFieldMatchStatus.Ambiguous),
            _ => new FreeFieldCandidateDto(recordId, code, nameAr, null, null, FreeFieldMatchStatus.NoMatch)
        };
    }
}

/// <summary>Case/whitespace-insensitive exact match on either name column — shared by the report and the auto-match command so the two can never disagree.</summary>
internal static class FreeFieldNameMatcher
{
    public static List<EmployeeNameLookup> FindMatches(string nameAr, string nameEn, long? companyId, IReadOnlyList<EmployeeNameLookup> employees)
    {
        var ar = nameAr.Trim();
        var en = nameEn.Trim();
        return employees
            .Where(e => e.CompanyId == companyId)
            .Where(e => string.Equals(e.NameAr.Trim(), ar, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(e.NameEn.Trim(), en, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public sealed record FreeFieldAutoMatchResultDto(int CustodyOfficersLinked, int MaintenanceRequestsLinked, int MaintenanceSchedulesLinked);

/// <summary>Idempotent: only touches rows that are still unlinked and have exactly one exact-name match. Running it again links nothing new unless data changed.</summary>
public sealed record ApplyFreeFieldAutoMatchCommand : IRequest<FreeFieldAutoMatchResultDto>;

public sealed class ApplyFreeFieldAutoMatchCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ApplyFreeFieldAutoMatchCommand, FreeFieldAutoMatchResultDto>
{
    public async Task<FreeFieldAutoMatchResultDto> Handle(ApplyFreeFieldAutoMatchCommand request, CancellationToken cancellationToken)
    {
        var employees = await GetFreeFieldDiagnosticReportQueryHandler.LoadActiveEmployees(db, cancellationToken);
        if (employees.Count == 0)
        {
            return new FreeFieldAutoMatchResultDto(0, 0, 0);
        }

        var officersLinked = 0;
        var officers = await db.CustodyOfficers.Where(o => o.EmployeeId == null).ToListAsync(cancellationToken);
        foreach (var officer in officers)
        {
            var matches = FreeFieldNameMatcher.FindMatches(officer.NameAr, officer.NameEn, officer.CompanyId, employees);
            if (matches.Count == 1)
            {
                officer.EmployeeId = matches[0].Id;
                officersLinked++;
            }
        }

        var requestsLinked = 0;
        var requests = await db.MaintenanceRequests.Where(r => r.TechnicianId == null && r.TechnicianName != null).ToListAsync(cancellationToken);
        foreach (var maintenanceRequest in requests)
        {
            var matches = FreeFieldNameMatcher.FindMatches(maintenanceRequest.TechnicianName!, maintenanceRequest.TechnicianName!, maintenanceRequest.CompanyId, employees);
            if (matches.Count == 1)
            {
                maintenanceRequest.TechnicianId = matches[0].Id;
                requestsLinked++;
            }
        }

        var schedulesLinked = 0;
        var schedules = await db.MaintenanceSchedules.Where(s => s.TechnicianId == null && s.TechnicianName != null).ToListAsync(cancellationToken);
        foreach (var schedule in schedules)
        {
            var matches = FreeFieldNameMatcher.FindMatches(schedule.TechnicianName!, schedule.TechnicianName!, schedule.CompanyId, employees);
            if (matches.Count == 1)
            {
                schedule.TechnicianId = matches[0].Id;
                schedulesLinked++;
            }
        }

        if (officersLinked > 0 || requestsLinked > 0 || schedulesLinked > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new FreeFieldAutoMatchResultDto(officersLinked, requestsLinked, schedulesLinked);
    }
}

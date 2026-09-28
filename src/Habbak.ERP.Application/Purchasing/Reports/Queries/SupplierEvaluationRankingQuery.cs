using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Reports.Queries;

// ---- Report #7: تقييم أداء الموردين (Supplier Evaluation Ranking) — ثابت ----

public sealed class SupplierEvaluationRankingRowDto
{
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required int EvaluationCount { get; init; }
    public required decimal AverageQualityScore { get; init; }
    public required decimal AverageDeliveryTimeScore { get; init; }
    public required decimal AverageQuantityComplianceScore { get; init; }
    public required decimal AverageOverallScore { get; init; }
    public required DateOnly LatestEvaluationDate { get; init; }
}

/// <summary>Aggregates every SupplierEvaluation row per supplier into a ranking — distinct from
/// screen #12's own List/Edit grid, which shows one row per evaluation rather than a per-supplier
/// average.</summary>
public sealed record GetSupplierEvaluationRankingReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<SupplierEvaluationRankingRowDto>>;

public sealed class GetSupplierEvaluationRankingReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierEvaluationRankingReportQuery, IReadOnlyList<SupplierEvaluationRankingRowDto>>
{
    public async Task<IReadOnlyList<SupplierEvaluationRankingRowDto>> Handle(GetSupplierEvaluationRankingReportQuery request, CancellationToken cancellationToken) =>
        await db.SupplierEvaluations
            .AsNoTracking()
            .Include(e => e.Supplier)
            .Where(e => e.EvaluationDate >= request.From && e.EvaluationDate <= request.To)
            .GroupBy(e => new { e.SupplierId, e.Supplier!.Code, e.Supplier!.NameAr })
            .Select(g => new SupplierEvaluationRankingRowDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierCode = g.Key.Code,
                SupplierNameAr = g.Key.NameAr,
                EvaluationCount = g.Count(),
                AverageQualityScore = g.Average(e => e.QualityScore),
                AverageDeliveryTimeScore = g.Average(e => e.DeliveryTimeScore),
                AverageQuantityComplianceScore = g.Average(e => e.QuantityComplianceScore),
                AverageOverallScore = g.Average(e => e.OverallScore),
                LatestEvaluationDate = g.Max(e => e.EvaluationDate)
            })
            .OrderByDescending(r => r.AverageOverallScore)
            .ToListAsync(cancellationToken);
}

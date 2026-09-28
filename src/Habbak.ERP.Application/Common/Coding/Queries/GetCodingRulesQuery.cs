using Habbak.ERP.Application.Common.Coding.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Coding.Queries;

/// <summary>GET /api/v1/settings/coding-rules — every code-bearing screen, merged with the
/// current company's overrides (or that screen's built-in default when none exists yet).</summary>
public sealed record GetCodingRulesQuery(string Lang) : IRequest<List<CodingRuleDto>>;

public sealed class GetCodingRulesQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetCodingRulesQuery, List<CodingRuleDto>>
{
    public async Task<List<CodingRuleDto>> Handle(GetCodingRulesQuery request, CancellationToken cancellationToken)
    {
        var useEnglish = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase);

        var rules = await db.CodingRules
            .AsNoTracking()
            .Where(r => r.CompanyId == currentCompanyContext.CompanyId)
            .ToListAsync(cancellationToken);

        var byScreen = rules.ToDictionary(r => r.ScreenCode);

        return ScreenCodeCatalog.All.Select(def =>
        {
            var rule = byScreen.GetValueOrDefault(def.ScreenCode);
            return new CodingRuleDto
            {
                ScreenCode = def.ScreenCode,
                ScreenLabel = useEnglish ? def.LabelEn : def.LabelAr,
                IsAutomatic = rule?.IsAutomatic ?? def.DefaultIsAutomatic,
                Format = (rule?.Format ?? def.DefaultFormat).ToString(),
                Prefix = rule?.Prefix ?? def.DefaultPrefix,
                SequenceLength = rule?.SequenceLength ?? def.DefaultSequenceLength,
                IsAttachmentMandatory = rule?.IsAttachmentMandatory ?? false,
                IsDescriptionMandatory = rule?.IsDescriptionMandatory ?? false
            };
        }).ToList();
    }
}

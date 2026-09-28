using Habbak.ERP.Application.Common.Coding.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Coding.Queries;

/// <summary>GET /api/v1/settings/coding-rules/{screenCode} — used by a single create-form to
/// decide whether to show a manual Code input.</summary>
public sealed record GetCodingRuleQuery(string ScreenCode, string Lang) : IRequest<CodingRuleDto>;

public sealed class GetCodingRuleQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetCodingRuleQuery, CodingRuleDto>
{
    public async Task<CodingRuleDto> Handle(GetCodingRuleQuery request, CancellationToken cancellationToken)
    {
        var useEnglish = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase);
        var def = ScreenCodeCatalog.Find(request.ScreenCode);

        var rule = await db.CodingRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ScreenCode == request.ScreenCode && r.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        return new CodingRuleDto
        {
            ScreenCode = request.ScreenCode,
            ScreenLabel = useEnglish ? (def?.LabelEn ?? request.ScreenCode) : (def?.LabelAr ?? request.ScreenCode),
            IsAutomatic = rule?.IsAutomatic ?? def?.DefaultIsAutomatic ?? false,
            Format = (rule?.Format ?? def?.DefaultFormat ?? Domain.Common.CodeFormat.LettersAndNumbers).ToString(),
            Prefix = rule?.Prefix ?? def?.DefaultPrefix,
            SequenceLength = rule?.SequenceLength ?? def?.DefaultSequenceLength ?? 5,
            IsAttachmentMandatory = rule?.IsAttachmentMandatory ?? false,
            IsDescriptionMandatory = rule?.IsDescriptionMandatory ?? false
        };
    }
}

using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Labels.Queries;

/// <summary>GET /api/v1/field-labels?screenCode=... (00-System-Wide-Corrections-01.md, section 4.3).</summary>
public sealed record GetFieldLabelsQuery(string ScreenCode, string Lang) : IRequest<Dictionary<string, string>>;

public sealed class GetFieldLabelsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetFieldLabelsQuery, Dictionary<string, string>>
{
    public async Task<Dictionary<string, string>> Handle(GetFieldLabelsQuery request, CancellationToken cancellationToken)
    {
        var useEnglish = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase);

        var rows = await db.FieldLabels
            .AsNoTracking()
            .Where(f => f.ScreenCode == request.ScreenCode)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(f => f.FieldCode, f => useEnglish ? f.NameEn : f.NameAr);
    }
}

using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting.Resolvers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Screens;

public sealed record PostingScreenFieldDto(
    string Name, string LabelAr, string LabelEn, string Kind, decimal? Sample, string EntityType, bool IsContext, IReadOnlyList<string>? Choices);

public sealed record PostingScreenGroupDto(string Name, string LabelAr, string LabelEn, decimal Sample);

public sealed record PostingScreenResolverDto(string Key, string Kind, string RequiredField);

public sealed record PostingRelatedFieldDto(string Name, string LabelAr, string LabelEn, string EntityType);

public sealed record PostingRelatedEntityDto(string Name, string LabelAr, string LabelEn, IReadOnlyList<PostingRelatedFieldDto> Fields);

public sealed record PostingScreenTemplateDto(
    long Id, string NameAr, string TriggerType, string? TriggerFieldName, string? TriggerFieldValue,
    int ExecutionOrder, int VersionNumber, bool IsActive, int LineCount);

public sealed record PostingScreenDto(
    string ScreenCode,
    string NameAr,
    string NameEn,
    string SourceModule,
    string WhenAr,
    bool CanMoveStock,
    bool IsPosting,
    int DefaultTemplateCount,
    IReadOnlyList<PostingScreenFieldDto> Fields,
    IReadOnlyList<PostingScreenGroupDto> Groups,
    IReadOnlyList<PostingScreenResolverDto> Resolvers,
    IReadOnlyList<PostingRelatedEntityDto> RelatedEntities,
    IReadOnlyList<PostingScreenTemplateDto> Templates);

/// <summary>
/// Every screen that can post, with what its templates may use and the templates it carries. The
/// resolver list is narrowed to the ones this screen can feed, so the editor never offers a
/// supplier's account on a sales invoice.
/// </summary>
public sealed record GetPostingScreensQuery : IRequest<IReadOnlyList<PostingScreenDto>>;

public sealed class GetPostingScreensQueryHandler(IApplicationDbContext db, IPostingResolverRegistry resolvers)
    : IRequestHandler<GetPostingScreensQuery, IReadOnlyList<PostingScreenDto>>
{
    public async Task<IReadOnlyList<PostingScreenDto>> Handle(GetPostingScreensQuery request, CancellationToken cancellationToken)
    {
        var templates = await db.PostingTemplates.AsNoTracking()
            .Where(t => t.IsCurrentVersion)
            .OrderBy(t => t.ExecutionOrder).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.ScreenCode,
                Dto = new PostingScreenTemplateDto(
                    t.Id, t.NameAr, t.TriggerType.ToString(), t.TriggerFieldName, t.TriggerFieldValue,
                    t.ExecutionOrder, t.VersionNumber, t.IsActive, t.Lines.Count)
            })
            .ToListAsync(cancellationToken);

        return PostingScreenCatalog.All.Select(screen =>
        {
            var fieldNames = screen.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var screenResolvers = resolvers.AccountResolverKeys
                .Select(k => resolvers.FindAccountResolver(k)!)
                .Where(r => fieldNames.Contains(r.RequiredField))
                .Select(r => new PostingScreenResolverDto(r.Key, "Account", r.RequiredField))
                .Concat(resolvers.CostCenterResolverKeys
                    .Select(k => resolvers.FindCostCenterResolver(k)!)
                    .Where(r => fieldNames.Contains(r.RequiredField))
                    .Select(r => new PostingScreenResolverDto(r.Key, "CostCenter", r.RequiredField)))
                .OrderBy(r => r.Key)
                .ToList();

            var screenTemplates = templates.Where(t => t.ScreenCode == screen.ScreenCode).Select(t => t.Dto).ToList();

            return new PostingScreenDto(
                screen.ScreenCode, screen.NameAr, screen.NameEn, screen.SourceModule.ToString(), screen.WhenAr, screen.CanMoveStock,
                screenTemplates.Any(t => t.IsActive),
                screen.DefaultTemplates.Count,
                screen.Fields.Select(f => new PostingScreenFieldDto(
                    f.Name, f.LabelAr, f.LabelEn, f.Kind.ToString(), f.Sample, f.EntityType.ToString(), f.IsContext, f.Choices)).ToList(),
                screen.Groups.Select(g => new PostingScreenGroupDto(g.Name, g.LabelAr, g.LabelEn, g.Sample)).ToList(),
                screenResolvers,
                screen.RelatedEntities.Select(r => new PostingRelatedEntityDto(
                    r.Name, r.LabelAr, r.LabelEn,
                    r.Fields.Select(f => new PostingRelatedFieldDto(f.Name, f.LabelAr, f.LabelEn, f.EntityType.ToString())).ToList())).ToList(),
                screenTemplates);
        }).ToList();
    }
}

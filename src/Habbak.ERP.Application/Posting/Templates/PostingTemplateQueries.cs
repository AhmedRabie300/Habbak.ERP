using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting.Resolvers;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Posting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Templates;

public sealed class PostingTemplateListItemDto
{
    public required long Id { get; init; }
    public required string ScreenCode { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string TriggerType { get; init; }
    public string? TriggerFieldName { get; init; }
    public string? TriggerFieldValue { get; init; }
    public required int ExecutionOrder { get; init; }
    public required int VersionNumber { get; init; }
    public required bool IsCurrentVersion { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsSystemTemplate { get; init; }
    public required int LineCount { get; init; }
}

public sealed class PostingTemplateCostCenterDto
{
    public required long CostCenterDimensionId { get; init; }
    public required string SourceType { get; init; }
    public long? FixedValueId { get; init; }
    public string? ValueFieldName { get; init; }
    public string? ValueResolverKey { get; init; }
    public string? RelatedEntityType { get; init; }
    public string? RelatedEntityField { get; init; }
    public string? ContextKey { get; init; }
    public required int DisplayOrder { get; init; }
}

public sealed class PostingTemplateLineDto
{
    public required int LineNumber { get; init; }
    public required string Direction { get; init; }
    public required string AccountSourceType { get; init; }
    public long? FixedAccountId { get; init; }
    public string? AccountFieldName { get; init; }
    public string? AccountResolverKey { get; init; }
    public required string AmountFormulaType { get; init; }
    public string? AmountFieldName { get; init; }
    public IReadOnlyList<string>? AmountFieldNames { get; init; }
    public decimal? AmountMultiplier { get; init; }
    public decimal? AmountPercentage { get; init; }
    public required string ConditionType { get; init; }
    public string? ConditionFieldName { get; init; }
    public string? ConditionFieldValue { get; init; }
    public string? LineDescription { get; init; }
    public required IReadOnlyList<PostingTemplateCostCenterDto> CostCenters { get; init; }
}

public sealed class PostingTemplateDetailDto
{
    public required long Id { get; init; }
    public required string ScreenCode { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? Description { get; init; }
    public required string TriggerType { get; init; }
    public string? TriggerFieldName { get; init; }
    public string? TriggerFieldValue { get; init; }
    public required int ExecutionOrder { get; init; }
    public required int VersionNumber { get; init; }
    public long? PreviousVersionId { get; init; }
    public required bool IsCurrentVersion { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsSystemTemplate { get; init; }
    public required IReadOnlyList<PostingTemplateLineDto> Lines { get; init; }
}

/// <summary>Current versions by default; <paramref name="IncludeHistory"/> adds every older version too.</summary>
public sealed record GetPostingTemplatesListQuery(string? ScreenCode = null, bool IncludeHistory = false)
    : IRequest<IReadOnlyList<PostingTemplateListItemDto>>;

public sealed class GetPostingTemplatesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPostingTemplatesListQuery, IReadOnlyList<PostingTemplateListItemDto>>
{
    public async Task<IReadOnlyList<PostingTemplateListItemDto>> Handle(GetPostingTemplatesListQuery request, CancellationToken cancellationToken) =>
        await db.PostingTemplates
            .AsNoTracking()
            .Where(t => request.IncludeHistory || t.IsCurrentVersion)
            .Where(t => request.ScreenCode == null || t.ScreenCode == request.ScreenCode)
            .OrderBy(t => t.ScreenCode).ThenBy(t => t.ExecutionOrder).ThenByDescending(t => t.VersionNumber)
            .Select(t => new PostingTemplateListItemDto
            {
                Id = t.Id,
                ScreenCode = t.ScreenCode,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                TriggerType = t.TriggerType.ToString(),
                TriggerFieldName = t.TriggerFieldName,
                TriggerFieldValue = t.TriggerFieldValue,
                ExecutionOrder = t.ExecutionOrder,
                VersionNumber = t.VersionNumber,
                IsCurrentVersion = t.IsCurrentVersion,
                IsActive = t.IsActive,
                IsSystemTemplate = t.IsSystemTemplate,
                LineCount = t.Lines.Count
            })
            .ToListAsync(cancellationToken);
}

public sealed record GetPostingTemplateByIdQuery(long Id) : IRequest<PostingTemplateDetailDto>;

public sealed class GetPostingTemplateByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPostingTemplateByIdQuery, PostingTemplateDetailDto>
{
    public async Task<PostingTemplateDetailDto> Handle(GetPostingTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var t = await db.PostingTemplates
            .AsNoTracking()
            .Include(x => x.Lines).ThenInclude(l => l.CostCenters)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PostingTemplate), request.Id);

        return new PostingTemplateDetailDto
        {
            Id = t.Id,
            ScreenCode = t.ScreenCode,
            NameAr = t.NameAr,
            NameEn = t.NameEn,
            Description = t.Description,
            TriggerType = t.TriggerType.ToString(),
            TriggerFieldName = t.TriggerFieldName,
            TriggerFieldValue = t.TriggerFieldValue,
            ExecutionOrder = t.ExecutionOrder,
            VersionNumber = t.VersionNumber,
            PreviousVersionId = t.PreviousVersionId,
            IsCurrentVersion = t.IsCurrentVersion,
            IsActive = t.IsActive,
            IsSystemTemplate = t.IsSystemTemplate,
            Lines = t.Lines.OrderBy(l => l.LineNumber).Select(l => new PostingTemplateLineDto
            {
                LineNumber = l.LineNumber,
                Direction = l.Direction.ToString(),
                AccountSourceType = l.AccountSourceType.ToString(),
                FixedAccountId = l.FixedAccountId,
                AccountFieldName = l.AccountFieldName,
                AccountResolverKey = l.AccountResolverKey,
                AmountFormulaType = l.AmountFormulaType.ToString(),
                AmountFieldName = l.AmountFieldName,
                AmountFieldNames = l.AmountFieldNames == null ? null : l.AmountFieldNames.Split(',', StringSplitOptions.RemoveEmptyEntries),
                AmountMultiplier = l.AmountMultiplier,
                AmountPercentage = l.AmountPercentage,
                ConditionType = l.ConditionType.ToString(),
                ConditionFieldName = l.ConditionFieldName,
                ConditionFieldValue = l.ConditionFieldValue,
                LineDescription = l.LineDescription,
                CostCenters = l.CostCenters.OrderBy(c => c.DisplayOrder).Select(c => new PostingTemplateCostCenterDto
                {
                    CostCenterDimensionId = c.CostCenterDimensionId,
                    SourceType = c.SourceType.ToString(),
                    FixedValueId = c.FixedValueId,
                    ValueFieldName = c.ValueFieldName,
                    ValueResolverKey = c.ValueResolverKey,
                    RelatedEntityType = c.RelatedEntityType,
                    RelatedEntityField = c.RelatedEntityField,
                    ContextKey = c.ContextKey,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
            }).ToList()
        };
    }
}

/// <summary>
/// The closed lists a template can be built from. The editor offers only these — which is the
/// whole design (spec section 4): nothing typed in the editor is ever interpreted as an expression.
/// </summary>
public sealed class PostingEngineCatalogDto
{
    public required IReadOnlyList<string> AmountFormulaTypes { get; init; }
    public required IReadOnlyList<string> ConditionTypes { get; init; }
    public required IReadOnlyList<string> AccountSourceTypes { get; init; }
    public required IReadOnlyList<string> CostCenterSourceTypes { get; init; }
    public required IReadOnlyList<string> CompanyAccountRoles { get; init; }
    public required IReadOnlyList<string> AccountResolverKeys { get; init; }
    public required IReadOnlyList<string> CostCenterResolverKeys { get; init; }
}

public sealed record GetPostingEngineCatalogQuery : IRequest<PostingEngineCatalogDto>;

public sealed class GetPostingEngineCatalogQueryHandler(IPostingResolverRegistry resolvers)
    : IRequestHandler<GetPostingEngineCatalogQuery, PostingEngineCatalogDto>
{
    public Task<PostingEngineCatalogDto> Handle(GetPostingEngineCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new PostingEngineCatalogDto
        {
            AmountFormulaTypes = Enum.GetNames<AmountFormulaType>(),
            ConditionTypes = Enum.GetNames<ConditionType>(),
            AccountSourceTypes = Enum.GetNames<AccountSourceType>(),
            CostCenterSourceTypes = Enum.GetNames<CostCenterSourceType>(),
            CompanyAccountRoles = Enum.GetNames<CompanyAccountRole>(),
            AccountResolverKeys = resolvers.AccountResolverKeys.Order().ToList(),
            CostCenterResolverKeys = resolvers.CostCenterResolverKeys.Order().ToList()
        });
}

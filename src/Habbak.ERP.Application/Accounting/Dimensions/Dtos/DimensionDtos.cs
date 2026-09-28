using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.Application.Accounting.Dimensions.Dtos;

public sealed class DimensionDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>None: values are managed here (CostCenterDimensionValue). Otherwise, values are
    /// that other screen's own active records instead (e.g. Branch — the Branches screen).</summary>
    public required CostCenterLinkedEntityType LinkedEntityType { get; init; }
}

public sealed class DimensionValueDto
{
    public required long Id { get; init; }
    public required long CostCenterDimensionId { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public long? ParentId { get; init; }
    public required int Level { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class AccountDimensionLinkDto
{
    public required long Id { get; init; }
    public required long AccountId { get; init; }
    public required long CostCenterDimensionId { get; init; }
    public required string DimensionNameAr { get; init; }
    public required string DimensionNameEn { get; init; }
    public required int DisplayOrder { get; init; }
    public required bool IsMandatory { get; init; }
}

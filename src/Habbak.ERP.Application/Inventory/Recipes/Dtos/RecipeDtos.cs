namespace Habbak.ERP.Application.Inventory.Recipes.Dtos;

public sealed class RecipeListItemDto
{
    public required long Id { get; init; }
    public required string RecipeFamilyCode { get; init; }
    public required int VersionNumber { get; init; }
    public required long OutputItemId { get; init; }
    public required string OutputItemCode { get; init; }
    public required string OutputItemNameAr { get; init; }
    public required decimal OutputQuantity { get; init; }
    public required bool IsCurrentVersion { get; init; }
    public required string Status { get; init; }
}

public sealed class RecipeDetailDto
{
    public required long Id { get; init; }
    public required string RecipeFamilyCode { get; init; }
    public required int VersionNumber { get; init; }
    public long? PreviousVersionId { get; init; }
    public required bool IsCurrentVersion { get; init; }
    public required long OutputItemId { get; init; }
    public required string OutputItemCode { get; init; }
    public required string OutputItemNameAr { get; init; }
    public required decimal OutputQuantity { get; init; }
    public required decimal WastePercentage { get; init; }
    public required string Status { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }

    /// <summary>Σ(RecipeLine.Quantity × ComponentItem.StandardCost) — screen #17's "computed cost"
    /// (module doc, section 5) shown alongside the approve/reject decision.</summary>
    public required decimal EstimatedComponentCost { get; init; }

    public required decimal CostPerOutputUnit { get; init; }

    public required IReadOnlyList<RecipeLineDto> Lines { get; init; }
}

public sealed class RecipeLineDto
{
    public long? Id { get; init; }
    public required long ComponentItemId { get; init; }
    public required string ComponentItemCode { get; init; }
    public required string ComponentItemNameAr { get; init; }
    public required decimal Quantity { get; init; }

    /// <summary>The unit Quantity is in; UnitFactor base units make one of it.</summary>
    public required long UnitId { get; init; }
    public string? UnitCode { get; init; }
    public string? UnitNameAr { get; init; }
    public required decimal UnitFactor { get; init; }

    /// <summary>Per base unit of the component.</summary>
    public decimal? ComponentStandardCost { get; init; }
}

/// <summary>Quantity is in UnitId — the component's base unit when left out.</summary>
public sealed record RecipeLineInput(long ComponentItemId, decimal Quantity, long? UnitId = null);

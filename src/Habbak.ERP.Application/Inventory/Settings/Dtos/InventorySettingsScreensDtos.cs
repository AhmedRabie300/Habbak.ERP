using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Inventory.Settings.Dtos;

/// <summary>DTOs for the three section-2.7 settings screens (#21-23) — kept in one file since
/// each is a tiny, self-contained shape with no cross-references between them.</summary>
public sealed record ShortagePolicyDto(bool AllowOverrideOnShortage, bool RequiresApprovalForOverride);

public sealed record InventorySettingsDto(int SlowMovingThresholdDays);

public sealed class ProductionSalesModeSettingDto
{
    public required long Id { get; init; }
    public required string ScopeType { get; init; }
    public long? ScopeId { get; init; }
    public required string Mode { get; init; }
}

public sealed record CreateProductionSalesModeSettingInput(SettingScopeType ScopeType, long? ScopeId, ProductionSalesMode Mode);

public sealed record UpdateProductionSalesModeSettingInput(ProductionSalesMode Mode);

using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// وصفة / BOM (02-Module-Inventory-Manufacturing.md, section 2.6). Versioned: editing an Approved
/// recipe never touches its row — CreateNewRecipeVersionCommand clones a new Draft row sharing the
/// same RecipeFamilyCode with VersionNumber+1 and PreviousVersionId pointing back (rule 31), so every
/// historical ProductionOrder.RecipeId keeps referencing the exact version that was current when it
/// ran, even after a newer version is later approved.
/// </summary>
public class Recipe : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    /// <summary>Fixed across every version of the same recipe — generated via ICodeGenerator only
    /// on the very first version; later versions copy it unchanged (unlike Id, which is per-row).</summary>
    public string RecipeFamilyCode { get; set; } = null!;

    public int VersionNumber { get; set; } = 1;
    public long? PreviousVersionId { get; set; }
    public Recipe? PreviousVersion { get; set; }

    /// <summary>Exactly one row per RecipeFamilyCode holds true at any time — ApproveRecipeCommand
    /// flips every sibling version to false before setting this one true.</summary>
    public bool IsCurrentVersion { get; set; } = true;

    /// <summary>Fixed across every version — must be SemiFinished or FinishedGood (rule 27: never
    /// RawMaterial, a raw material is purchased only, never produced by a recipe).</summary>
    public long OutputItemId { get; set; }
    public Item? OutputItem { get; set; }

    public decimal OutputQuantity { get; set; }
    public decimal WastePercentage { get; set; }

    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;
    public long? ApprovalInstanceId { get; set; }
    public DateOnly EffectiveFromDate { get; set; }

    public ICollection<RecipeLine> Lines { get; set; } = new List<RecipeLine>();
}

/// <summary>مكوّن الوصفة — ComponentItemId ممكن يكون هو نفسه OutputItemId لوصفة تانية، بما يدعم
/// وصفات متعددة المستويات (رقاقة روبوستا محمصة تدخل في خلطة هاوس بلند)، مع منع أي حلقة دائرية عند
/// الحفظ (rule 13, enforced in CreateRecipeCommand/UpdateRecipeCommand).</summary>
public class RecipeLine : AuditableEntity
{
    public long RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public long ComponentItemId { get; set; }
    public Item? ComponentItem { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// The unit the quantity is in — the item's base unit or one of its ItemUnitConversion units
    /// (Remarks3). <see cref="UnitFactor"/> is how many base units one of it holds, copied when the
    /// line is saved so a later change to the item's conversions does not rewrite this document.
    /// </summary>
    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public decimal UnitFactor { get; set; } = 1;
}

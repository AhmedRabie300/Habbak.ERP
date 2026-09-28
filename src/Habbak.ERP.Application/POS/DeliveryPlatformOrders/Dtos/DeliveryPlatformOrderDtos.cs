namespace Habbak.ERP.Application.POS.DeliveryPlatformOrders.Dtos;

/// <summary>عنصر موحَّد لشاشة #10 (طلبات الدليفري) — يجمع طلبات المنصات الخارجية (`DeliveryPlatformOrder`)
/// مع الدليفري الداخلي (`Check` بنوع `Delivery` غير مرتبط بأي `DeliveryPlatformOrder`) في قائمة واحدة،
/// IsExternalPlatform بيفرّق بينهم للعرض فقط.</summary>
public sealed class DeliveryOrderListItemDto
{
    public required long CheckId { get; init; }
    public required string CheckCode { get; init; }
    public required string Status { get; init; }
    public required bool IsExternalPlatform { get; init; }
    public string? PlatformName { get; init; }
    public string? PlatformOrderId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public string? DeliveryAddress { get; init; }
    public required int LineCount { get; init; }
    public required decimal Total { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

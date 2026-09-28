namespace Habbak.ERP.Application.POS.Tables.Dtos;

public sealed class TableDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required long BranchId { get; init; }
    public required string Status { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>شاشة الطرابيزات التفاعلية (screen #8) — كل طرابيزة مع ملخص الشيك المفتوح/المعلَّق
/// المرتبط بيها لو موجود، عشان الضغط عليها يعرف يقرر يسترجع ولا يفتح شيك جديد (قاعدة 7).</summary>
public sealed class TableBoardItemDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required string Status { get; init; }
    public long? OpenCheckId { get; init; }
    public string? OpenCheckCode { get; init; }
    public string? OpenCheckStatus { get; init; }
    public decimal? OpenCheckTotal { get; init; }
}

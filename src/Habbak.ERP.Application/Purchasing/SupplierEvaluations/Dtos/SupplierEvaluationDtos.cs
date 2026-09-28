namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Dtos;

public sealed class SupplierEvaluationListItemDto
{
    public required long Id { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required DateOnly EvaluationDate { get; init; }
    public required decimal OverallScore { get; init; }
}

public sealed class SupplierEvaluationDetailDto
{
    public required long Id { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required DateOnly EvaluationDate { get; init; }
    public required decimal QualityScore { get; init; }
    public required decimal DeliveryTimeScore { get; init; }
    public required decimal QuantityComplianceScore { get; init; }
    public required decimal OverallScore { get; init; }
    public string? Notes { get; init; }

    /// <summary>Base64-encoded RowVersion — sent back on PUT for optimistic concurrency.</summary>
    public required string RowVersion { get; init; }
}

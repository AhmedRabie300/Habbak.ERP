namespace Habbak.ERP.API.Contracts;

/// <summary>The one error shape every endpoint returns (00-Frontend-Specs.md, section 12.1).</summary>
public sealed class ErrorResponse
{
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<ErrorDetail>? Details { get; init; }
    public required string CorrelationId { get; init; }
}

public sealed record ErrorDetail(string Field, string Message);

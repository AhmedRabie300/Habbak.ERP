namespace Habbak.ERP.Application.Common.Exceptions;

/// <summary>
/// Raised by IPostingService when a posting attempt violates one of the Accounting module's
/// business rules (01-Module-Accounting.md, section 3). Carries one or more machine-readable
/// codes plus user-facing (already-localized) messages, ready to be mapped onto the standard
/// VALIDATION_ERROR contract (00-Frontend-Specs.md, section 12.1) once the API layer exists.
/// </summary>
public sealed class PostingValidationException(IReadOnlyList<PostingValidationError> errors)
    : Exception(BuildMessage(errors))
{
    public IReadOnlyList<PostingValidationError> Errors { get; } = errors;

    private static string BuildMessage(IReadOnlyList<PostingValidationError> errors) =>
        string.Join(" | ", errors.Select(e => $"[{e.Code}] {e.Message}"));
}

public sealed record PostingValidationError(string Code, string Message);

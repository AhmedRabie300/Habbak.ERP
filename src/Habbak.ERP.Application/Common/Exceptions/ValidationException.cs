using FluentValidation.Results;

namespace Habbak.ERP.Application.Common.Exceptions;

/// <summary>
/// Raised by ValidationBehavior when a command/query fails its FluentValidation rules.
/// Shaped to map directly onto the standard error contract (00-Frontend-Specs.md, section 12.1):
/// Errors' keys become "field", values become "message".
/// </summary>
public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}

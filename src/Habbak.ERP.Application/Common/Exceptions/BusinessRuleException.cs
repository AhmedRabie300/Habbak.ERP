namespace Habbak.ERP.Application.Common.Exceptions;

/// <summary>
/// A state/business-rule violation that is not a plain input-shape validation error (e.g.
/// "cannot post a voucher that is already Posted") — distinct from
/// <see cref="ValidationException"/> so the API layer can later map it to 409 Conflict instead
/// of 400 Bad Request.
/// </summary>
public sealed class BusinessRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

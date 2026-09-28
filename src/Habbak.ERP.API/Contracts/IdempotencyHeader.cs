using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Contracts;

/// <summary>
/// Reads the optional <c>Idempotency-Key</c> header for the posting endpoints, which take no body
/// to carry the key in the way POS's own commands do.
///
/// A client sends one only when it is replaying a request it is not sure landed — a retry after a
/// timeout, or an offline queue flushing. Without it these endpoints behave exactly as before, so
/// nothing existing has to change; with it, a second arrival of the same post returns the first
/// result instead of moving stock twice.
/// </summary>
public static class IdempotencyHeader
{
    public const string Name = "Idempotency-Key";

    public static Guid? Read(ControllerBase controller)
    {
        var raw = controller.Request.Headers[Name].FirstOrDefault();
        return Guid.TryParse(raw, out var key) ? key : null;
    }
}

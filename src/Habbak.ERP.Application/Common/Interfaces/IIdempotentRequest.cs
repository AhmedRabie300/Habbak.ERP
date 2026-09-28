namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>Marker for a command whose accidental re-submission (Retry Policy, or a synced
/// Offline transaction arriving twice before local physical-delete confirms — 00-Project-Overview.md
/// section 14.1/14.2) must not run its side effects twice. IdempotencyBehavior short-circuits any
/// request carrying a key it has already processed, returning the original stored result instead of
/// re-executing the handler.</summary>
public interface IIdempotentRequest
{
    Guid? IdempotencyKey { get; }
}

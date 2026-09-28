namespace Habbak.ERP.Application.Common.Exceptions;

/// <summary>Sign-in or token failure → 401. The message is shown to the user as-is.</summary>
public sealed class AuthenticationFailedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Signed in, but not allowed to do this → 403.</summary>
public sealed class ForbiddenException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

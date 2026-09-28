using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Settings.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Settings;

/// <summary>
/// Sign-in. Login/refresh/logout are anonymous (they carry their own credentials); change-password
/// and "me" need a signed-in user — and are the only endpoints a token flagged
/// "password change required" can reach.
/// </summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/auth")]
public class AuthController(ISender mediator) : ControllerBase
{
    public sealed record LoginRequest(string Username, string Password, long? CompanyId, long? BranchId);
    public sealed record RefreshRequest(string RefreshToken, long? CompanyId, long? BranchId);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record TwoFactorLoginRequest(string ChallengeToken, string? Code, string? RecoveryCode);
    public sealed record TwoFactorCodeRequest(string Code);
    public sealed record DisableTwoFactorRequest(string Password, string? Code, string? RecoveryCode);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        // The session, or — when the user has two-factor sign-in — { twoFactorRequired, challengeToken, expiresAtUtc }.
        var result = await mediator.Send(new LoginCommand(request.Username, request.Password, request.CompanyId, request.BranchId), cancellationToken);
        return result.Session is { } session ? Ok(session) : Ok(result.TwoFactor);
    }

    [AllowAnonymous]
    [HttpPost("login/two-factor")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new VerifyTwoFactorLoginCommand(request.ChallengeToken, request.Code, request.RecoveryCode), cancellationToken));

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new RefreshSessionCommand(request.RefreshToken, request.CompanyId, request.BranchId), cancellationToken));

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken));

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMyAccessQuery(), cancellationToken));

    // ------------------------------------------------------------ the user's own two-factor sign-in

    [HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMyTwoFactorQuery(), cancellationToken));

    [HttpPost("two-factor/setup")]
    public async Task<IActionResult> BeginTwoFactorSetup(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new BeginTwoFactorSetupCommand(), cancellationToken));

    [HttpPost("two-factor/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new EnableTwoFactorCommand(request.Code), cancellationToken));

    [HttpPost("two-factor/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new DisableTwoFactorCommand(request.Password, request.Code, request.RecoveryCode), cancellationToken);
        return NoContent();
    }

    [HttpPost("two-factor/recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new RegenerateRecoveryCodesCommand(request.Code), cancellationToken));
}

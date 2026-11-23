using System.ComponentModel.DataAnnotations;
using Hrms.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hrms.Api.Controllers;

[ApiController, AllowAnonymous, EnableRateLimiting("public-forms"), Route("api/v1/auth/password-recovery")]
public sealed class PasswordRecoveryController(PasswordRecoveryService recovery) : ControllerBase
{
    public sealed record RequestCode([Required, EmailAddress, MaxLength(320)] string Email);
    public sealed record ResetPassword([Required, EmailAddress, MaxLength(320)] string Email,
        [Required, RegularExpression("^[0-9]{8}$")] string Code, [Required, StringLength(256, MinimumLength = 8)] string Password);

    [HttpPost("request")]
    public async Task<IActionResult> RequestAsync(RequestCode request, CancellationToken ct)
    {
        await recovery.RequestAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAsync(ResetPassword request, CancellationToken ct)
    {
        await recovery.ConfirmAsync(request.Email, request.Code, request.Password, ct);
        return NoContent();
    }
}

using Hrms.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/public")]
public sealed class PublicPlatformController(PlatformExperienceService service) : ControllerBase
{
    [HttpPost("trials"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Trial(PublicTrialRequest request, CancellationToken ct)
    { await service.RequestTrialAsync(request, ct); return Accepted(new { message = "Check your email to activate your company workspace." }); }

    [HttpPost("trials/resend"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Resend(ResendTrialRequest request, CancellationToken ct)
    { await service.ResendTrialAsync(request.Slug, request.Email, ct); return Accepted(new { message = "If the pending trial exists, another activation email has been queued." }); }

    [HttpPost("activate"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Activate(ActivateAccountRequest request, CancellationToken ct)
    { await service.ActivateAsync(request, ct); return Ok(new { message = "Account activated. You can now sign in." }); }

    [HttpPost("support-tickets"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Support(SupportTicketRequest request, CancellationToken ct)
    { var reference = await service.CreateTicketAsync(request, ct); return Accepted(new { reference }); }
}

public sealed record ResendTrialRequest(string Slug, string Email);

[ApiController, Route("api/v1/platform/support"), Authorize]
public sealed class PlatformSupportController(PlatformExperienceService service) : ControllerBase
{
    [HttpGet("tickets")]
    public Task<IReadOnlyList<SupportTicketDto>> Tickets(CancellationToken ct) => service.TicketsAsync(ct);

    [HttpPut("tickets/{id:guid}")]
    public Task<SupportTicketDto> Update(Guid id, SupportTicketUpdate request, CancellationToken ct) => service.UpdateTicketAsync(id, request, ct);

    [HttpGet("team")]
    public Task<IReadOnlyList<SupportTeamMember>> Team(CancellationToken ct) => service.TeamAsync(ct);

    [HttpPut("team/{id:guid}/access")]
    public async Task<IActionResult> Access(Guid id, SupportAccessUpdate request, CancellationToken ct)
    { await service.SetSupportAccessAsync(id, request.Enabled, ct); return NoContent(); }

    [HttpPost("team/invite")]
    public async Task<IActionResult> Invite(InviteSupportUserRequest request, CancellationToken ct)
    { await service.InviteSupportUserAsync(request, ct); return Accepted(new { message = "Support invitation queued." }); }
}

using Hrms.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/public")]
public sealed class PublicPlatformController(
    PlatformExperienceService service,
    IConfiguration configuration,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("trials"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Trial(PublicTrialRequest request, CancellationToken ct)
    { await service.RequestTrialAsync(request, CurrentApplicationBaseUrl(), ct); return Accepted(new { message = "Check your email to activate your company workspace." }); }

    [HttpPost("trials/resend"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Resend(ResendTrialRequest request, CancellationToken ct)
    { await service.ResendTrialAsync(request.Slug, request.Email, CurrentApplicationBaseUrl(), ct); return Accepted(new { message = "If the pending trial exists, another activation email has been queued." }); }

    [HttpPost("activate"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Activate(ActivateAccountRequest request, CancellationToken ct)
    { await service.ActivateAsync(request, ct); return Ok(new { message = "Account activated. You can now sign in." }); }

    [HttpPost("support-tickets"), AllowAnonymous, EnableRateLimiting("public-forms")]
    public async Task<IActionResult> Support(SupportTicketRequest request, CancellationToken ct)
    { var reference = await service.CreateTicketAsync(request, ct); return Accepted(new { reference }); }

    private string? CurrentApplicationBaseUrl()
    {
        var candidates = new[]
        {
            Request.Headers.Origin.FirstOrDefault(),
            Request.Headers.Referer.FirstOrDefault(),
            $"{Request.Scheme}://{Request.Host}"
        };
        var baseDomain = configuration["Tenancy:BaseDomain"]?.Trim().TrimEnd('.');
        foreach (var candidate in candidates)
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
            var isLocal = environment.IsDevelopment() && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
            var isConfiguredDomain = !string.IsNullOrWhiteSpace(baseDomain) && uri.Host.Equals(baseDomain, StringComparison.OrdinalIgnoreCase);
            if (isLocal || isConfiguredDomain) return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
        return null;
    }
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

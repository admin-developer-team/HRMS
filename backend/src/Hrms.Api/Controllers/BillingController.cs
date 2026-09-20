using Hrms.Application;
using Hrms.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[ApiController, Route("api/v1/billing")]
public sealed class BillingController(BillingService service) : ControllerBase
{
    [HttpGet("plans"), Authorize(Roles = "TENANT_ADMIN")]
    public IReadOnlyList<BillingPlan> Plans() => service.Plans();

    [HttpGet("status"), Authorize(Roles = "TENANT_ADMIN")]
    public Task<BillingStatus> Status(CancellationToken ct) => service.StatusAsync(ct);

    [HttpPost("checkout"), Authorize(Roles = "TENANT_ADMIN")]
    public Task<BillingCheckoutResult> Checkout(StartBillingRequest request, CancellationToken ct) => service.StartAsync(request.PlanCode, ct, request.Provider ?? "razorpay", request.CustomerPhone);

    [HttpPost("webhooks/razorpay"), AllowAnonymous, RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> RazorpayWebhook(CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, ct);
        await service.ProcessWebhookAsync(stream.ToArray(), Request.Headers["X-Razorpay-Signature"].ToString(),
            Request.Headers["X-Razorpay-Event-Id"].ToString(), ct);
        return Ok();
    }

    [HttpPost("webhooks/cashfree"), AllowAnonymous, RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> CashfreeWebhook(CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, ct);
        await service.ProcessCashfreeWebhookAsync(stream.ToArray(), Request.Headers["x-webhook-timestamp"].ToString(),
            Request.Headers["x-webhook-signature"].ToString(), ct);
        return Ok();
    }

    [HttpPost("cashfree-return"), AllowAnonymous]
    public IActionResult CashfreeReturn() => SeeOther("/billing");

    private IActionResult SeeOther(string destination)
    {
        Response.Headers.Location = destination;
        return StatusCode(303);
    }
}

public sealed record StartBillingRequest(string PlanCode, string? Provider = null, string? CustomerPhone = null);

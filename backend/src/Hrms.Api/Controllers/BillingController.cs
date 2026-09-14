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
    public Task<BillingCheckoutResult> Checkout(StartBillingRequest request, CancellationToken ct) => service.StartAsync(request.PlanCode, ct);

    [HttpPost("webhooks/razorpay"), AllowAnonymous, RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> RazorpayWebhook(CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, ct);
        await service.ProcessWebhookAsync(stream.ToArray(), Request.Headers["X-Razorpay-Signature"].ToString(),
            Request.Headers["X-Razorpay-Event-Id"].ToString(), ct);
        return Ok();
    }
}

public sealed record StartBillingRequest(string PlanCode);

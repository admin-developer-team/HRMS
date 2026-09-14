namespace Hrms.Application;

public sealed record BillingPlan(string Code, string Name, string Currency, long AmountMinor, int EmployeeLimit, string ProviderPlanId);
public sealed record BillingCheckoutResult(string SubscriptionId, string CheckoutUrl);
public sealed record BillingStatus(string PlanCode, bool Active, DateTimeOffset? EndsAt, string? PendingPlanCode, string? PendingStatus, string? PendingCheckoutUrl, bool TestMode);

// Keep provider-specific HTTP and signatures behind this boundary.
public interface ISubscriptionPaymentGateway
{
    string ProviderKey { get; }
    string PlanConfigurationKey { get; }
    bool IsTest { get; }
    Task<BillingCheckoutResult> CreateSubscriptionAsync(BillingPlan plan, Guid tenantId, CancellationToken ct);
    bool VerifyWebhook(ReadOnlySpan<byte> body, string signature);
}

namespace Hrms.Application;

public sealed record BillingPlan(string Code, string Name, string Currency, long AmountMinor, int EmployeeLimit, string ProviderPlanId, string Provider = "razorpay");
public sealed record BillingCheckoutResult(string SubscriptionId, string CheckoutUrl, string Provider = "razorpay", bool TestMode = false, string? PublicKeyId = null);
public sealed record ProviderSubscriptionStatus(string Status, DateTimeOffset? CurrentPeriodEnd, string PlanId, int PaidCount);
public sealed record BillingStatus(string PlanCode, bool Active, DateTimeOffset? EndsAt, string? PendingPlanCode, string? PendingStatus, string? PendingCheckoutUrl, bool TestMode, DateTimeOffset? TrialEndsAt, string? PendingProvider = null, string? PendingSubscriptionId = null, string? RazorpayKeyId = null, bool AdminManaged = false, long? CurrentAmountMinor = null, string? CurrentCurrency = null);

// Keep provider-specific HTTP and signatures behind this boundary.
public interface ISubscriptionPaymentGateway
{
    string ProviderKey { get; }
    string PlanConfigurationKey { get; }
    bool IsTest { get; }
    Task<BillingCheckoutResult> CreateSubscriptionAsync(BillingPlan plan, Guid tenantId, DateTimeOffset? firstChargeAt, CancellationToken ct);
    Task<ProviderSubscriptionStatus> GetSubscriptionAsync(string subscriptionId, CancellationToken ct);
    bool VerifyWebhook(ReadOnlySpan<byte> body, string signature);
}

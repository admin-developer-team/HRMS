using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Billing;

public sealed class BillingService(HrmsDbContext db, ICurrentTenant currentTenant, ISubscriptionPaymentGateway gateway, IConfiguration configuration,
    CashfreeSubscriptionGateway? cashfree = null, ICurrentUser? currentUser = null)
{
    public IReadOnlyList<BillingPlan> Plans()
    {
        var plans = new List<BillingPlan>();
        foreach (var section in configuration.GetSection("Billing:Plans").GetChildren().Where(section => section.Key == "starter"))
        {
            var amount = long.TryParse(section["AmountMinor"], out var parsedAmount) ? parsedAmount : 0;
            var limit = int.TryParse(section["EmployeeLimit"], out var parsedLimit) ? parsedLimit : 0;
            var currency = section["Currency"] ?? "INR";
            if (amount <= 0 || limit <= 0 || currency != "INR") continue;
            var razorpayId = section[$"{(gateway.IsTest ? "Test" : "Live")}:{gateway.PlanConfigurationKey}"];
            if (!string.IsNullOrWhiteSpace(razorpayId))
                plans.Add(new BillingPlan(section.Key, section["Name"] ?? section.Key, currency, amount, limit, razorpayId));
            if (cashfree?.IsConfigured == true && !string.IsNullOrWhiteSpace(cashfree.PlanId))
                plans.Add(new BillingPlan(section.Key, section["Name"] ?? section.Key, currency, amount, limit, cashfree.PlanId, "cashfree"));
        }
        return plans.OrderBy(x => x.AmountMinor).ThenBy(x => x.Provider).ToArray();
    }

    public async Task<BillingStatus> StatusAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.SingleAsync(x => x.Id == currentTenant.TenantId, ct);
        var checkout = await db.BillingCheckouts.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (checkout?.Status is "pending" or "authenticated" or "payment_pending" or "active")
        {
            var isCashfree = checkout.Provider.StartsWith("cashfree_", StringComparison.Ordinal);
            var remote = isCashfree
                ? await (cashfree ?? throw new InvalidOperationException("Cashfree is unavailable.")).GetSubscriptionAsync(checkout.ProviderSubscriptionId, ct)
                : await gateway.GetSubscriptionAsync(checkout.ProviderSubscriptionId, ct);
            if (remote.PlanId != checkout.ProviderPlanId) throw new DomainException("Billing plan mismatch.");
            if (remote.Status is "cancelled" or "customer_cancelled" or "completed" or "halted" or "expired" or "link_expired")
                checkout.Status = "cancelled";
            else if (remote.PaidCount > 0 && remote.CurrentPeriodEnd > DateTimeOffset.UtcNow)
            {
                await ActivatePaidPeriodAsync(checkout, remote.CurrentPeriodEnd.Value, ct);
            }
            else if (isCashfree && checkout.Status != "active")
                checkout.Status = remote.Status switch
                {
                    "active" or "bank_approval_pending" => "authenticated",
                    _ => checkout.Status
                };
            else if (!isCashfree && checkout.Status != "active" && remote.Status is ("authenticated" or "active" or "pending"))
                checkout.Status = remote.Status is "active" or "pending" ? "payment_pending" : remote.Status;
            await db.SaveChangesAsync(ct);
        }
        var subscription = await db.TenantSubscriptions.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var cashfreeProvider = cashfree?.ProviderKey ?? "disabled_cashfree";
        var active = await db.TenantSubscriptions.AnyAsync(x => x.IsActive
            && (x.BillingProvider == null || x.BillingProvider == gateway.ProviderKey || x.BillingProvider == cashfreeProvider)
            && x.StartsAt <= now && (!x.EndsAt.HasValue || x.EndsAt > now), ct);
        return new BillingStatus(subscription?.PlanCode ?? "unassigned", active,
            subscription?.EndsAt, checkout?.Status is "pending" or "authenticated" or "payment_pending" ? checkout.PlanCode : null,
            checkout?.Status,
            checkout?.Status == "pending" ? checkout.CheckoutUrl : null, checkout?.IsTest ?? false, tenant.TrialEndsAt,
            checkout?.Provider.StartsWith("cashfree_", StringComparison.Ordinal) == true ? "cashfree" : checkout is null ? null : "razorpay");
    }

    public async Task<BillingCheckoutResult> StartAsync(string planCode, CancellationToken ct, string provider = "razorpay", string? customerPhone = null)
    {
        var tenantId = currentTenant.TenantId ?? throw new UnauthorizedAccessException();
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct) ?? throw new KeyNotFoundException("Company not found.");
        if (tenant.Slug == "platform" || tenant.Status is TenantStatus.Suspended or TenantStatus.Cancelled)
            throw new UnauthorizedAccessException("This workspace cannot start a subscription.");
        if (provider is not ("razorpay" or "cashfree")) throw new DomainException("Choose Razorpay or Cashfree.");
        var plan = Plans().FirstOrDefault(x => x.Code == planCode && x.Provider == provider) ?? throw new DomainException("The selected payment provider is not configured.");
        if (!string.Equals(tenant.DefaultCurrency, plan.Currency, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("The company currency must match the billing plan currency.");
        var licensed = await db.Employees.CountAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, ct);
        if (plan.EmployeeLimit < licensed) throw new DomainException("This plan has fewer seats than the current licensed workforce.");
        var existing = await db.BillingCheckouts.Where(x => x.Status == "pending" || x.Status == "authenticated" || x.Status == "active" || x.Status == "payment_pending").OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing?.Status == "pending" && existing.Provider == (provider == "cashfree" ? cashfree?.ProviderKey : gateway.ProviderKey))
            return new BillingCheckoutResult(existing.ProviderSubscriptionId, existing.CheckoutUrl, provider, existing.IsTest);
        if (existing is not null)
            throw new DomainException("A recurring subscription already exists for this company. Contact the platform administrator before changing payment providers.");
        // Authorize the mandate now; Razorpay collects the first plan payment after the trial.
        // An already-expired trial starts billing immediately after authorization.
        var paidThrough = await db.TenantSubscriptions.Where(x => x.IsActive && x.PlanCode != "trial" && x.EndsAt != null)
            .OrderByDescending(x => x.EndsAt).Select(x => x.EndsAt).FirstOrDefaultAsync(ct);
        var firstChargeAt = paidThrough > DateTimeOffset.UtcNow.AddMinutes(5) ? paidThrough
            : tenant.Status == TenantStatus.Trial && tenant.TrialEndsAt > DateTimeOffset.UtcNow.AddMinutes(5)
                ? tenant.TrialEndsAt : null;
        BillingCheckoutResult result;
        if (provider == "cashfree")
        {
            var userId = currentUser?.UserId ?? throw new UnauthorizedAccessException();
            var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
            var baseUrl = TenantDomains.BaseUrlForTenant(configuration["Billing:Cashfree:ReturnBaseUrl"], configuration["Tenancy:BaseDomain"], tenant.Slug);
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var returnBase) || returnBase.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Configure a public application URL for Cashfree returns.");
            result = await (cashfree ?? throw new InvalidOperationException("Cashfree is unavailable."))
                .CreateSubscriptionAsync(plan, tenantId, user.DisplayName, user.Email, customerPhone ?? "",
                    baseUrl.TrimEnd('/') + "/api/v1/billing/cashfree-return", firstChargeAt, ct);
        }
        else result = await gateway.CreateSubscriptionAsync(plan, tenantId, firstChargeAt, ct);
        db.BillingCheckouts.Add(new BillingCheckout
        {
            TenantId = tenantId, Provider = provider == "cashfree" ? cashfree!.ProviderKey : gateway.ProviderKey, ProviderSubscriptionId = result.SubscriptionId,
            CheckoutUrl = result.CheckoutUrl,
            PlanCode = plan.Code, ProviderPlanId = plan.ProviderPlanId, Currency = plan.Currency,
            AmountMinor = plan.AmountMinor, EmployeeLimit = plan.EmployeeLimit, IsTest = provider == "cashfree" ? cashfree!.IsTest : gateway.IsTest
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task ProcessWebhookAsync(byte[] body, string signature, string eventId, CancellationToken ct)
    {
        if (!gateway.VerifyWebhook(body, signature)) throw new UnauthorizedAccessException("Invalid billing webhook signature.");
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 150) throw new DomainException("Missing billing event ID.");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var eventType = root.GetProperty("event").GetString() ?? "";
        var subscription = root.GetProperty("payload").GetProperty("subscription").GetProperty("entity");
        var providerSubscriptionId = subscription.GetProperty("id").GetString() ?? "";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.BillingWebhookEvents.IgnoreQueryFilters().AnyAsync(x => x.Provider == gateway.ProviderKey && x.ProviderEventId == eventId, ct)) return;
        var checkout = await db.BillingCheckouts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Provider == gateway.ProviderKey && x.ProviderSubscriptionId == providerSubscriptionId && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Billing checkout not found; retry the webhook after checkout is saved.");
        if (subscription.TryGetProperty("plan_id", out var planProperty) && planProperty.GetString() != checkout.ProviderPlanId)
            throw new DomainException("Billing plan mismatch.");
        currentTenant.Set(checkout.TenantId);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (eventType == "subscription.charged")
            {
                var endSeconds = subscription.GetProperty("current_end").GetInt64();
                var periodEnd = DateTimeOffset.FromUnixTimeSeconds(endSeconds);
                if (periodEnd <= now) throw new DomainException("The charged subscription has no future billing period.");
                if (root.GetProperty("payload").TryGetProperty("payment", out var paymentWrapper)
                    && paymentWrapper.TryGetProperty("entity", out var payment)
                    && payment.TryGetProperty("amount", out var amount) && amount.GetInt64() < checkout.AmountMinor)
                    throw new DomainException("The charged amount is lower than the configured plan price.");
                await ActivatePaidPeriodAsync(checkout, periodEnd, ct);
            }
            else if (eventType == "subscription.authenticated" && checkout.Status == "pending") checkout.Status = "authenticated";
            else if (eventType is "subscription.cancelled" or "subscription.completed" or "subscription.halted" or "subscription.expired")
            {
                checkout.Status = eventType["subscription.".Length..];
            }
            else if (eventType == "subscription.pending") checkout.Status = "payment_pending";
            db.BillingWebhookEvents.Add(new BillingWebhookEvent { TenantId = checkout.TenantId, Provider = gateway.ProviderKey, ProviderEventId = eventId, EventType = eventType });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { currentTenant.Clear(); }
    }

    public async Task ProcessCashfreeWebhookAsync(byte[] body, string timestamp, string signature, CancellationToken ct)
    {
        if (cashfree is null || !cashfree.VerifyWebhook(body, timestamp, signature))
            throw new UnauthorizedAccessException("Invalid Cashfree webhook signature.");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var eventType = root.GetProperty("type").GetString() ?? "";
        if (eventType is not ("SUBSCRIPTION_AUTH_STATUS" or "SUBSCRIPTION_STATUS_CHANGED" or "SUBSCRIPTION_PAYMENT_SUCCESS" or "SUBSCRIPTION_PAYMENT_FAILED")) return;
        var data = root.GetProperty("data");
        var details = eventType == "SUBSCRIPTION_STATUS_CHANGED" ? data.GetProperty("subscription_details") : data;
        var subscriptionId = details.GetProperty("subscription_id").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(subscriptionId)) throw new DomainException("Missing Cashfree subscription ID.");
        var eventTime = root.GetProperty("event_time").GetString() ?? "";
        if (!DateTimeOffset.TryParse(eventTime, out var occurredAt)) throw new DomainException("Missing Cashfree event time.");
        var paymentId = data.TryGetProperty("payment_id", out var paymentIdElement) ? paymentIdElement.GetString() : null;
        var eventId = eventType + ":" + (paymentId ?? eventTime + ":" + subscriptionId);
        if (eventId.Length > 150) throw new DomainException("Invalid Cashfree event ID.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.BillingWebhookEvents.IgnoreQueryFilters().AnyAsync(x => x.Provider == cashfree.ProviderKey && x.ProviderEventId == eventId, ct)) return;
        var checkout = await db.BillingCheckouts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Provider == cashfree.ProviderKey && x.ProviderSubscriptionId == subscriptionId && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Billing checkout not found; retry the webhook after checkout is saved.");
        currentTenant.Set(checkout.TenantId);
        try
        {
            if (eventType == "SUBSCRIPTION_PAYMENT_SUCCESS"
                && data.GetProperty("payment_status").GetString() == "SUCCESS"
                && data.GetProperty("payment_type").GetString() != "AUTH"
                && data.TryGetProperty("payment_schedule_date", out var scheduleDate)
                && scheduleDate.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(scheduleDate.GetString()))
            {
                var amount = data.GetProperty("payment_amount").GetDecimal();
                if (amount != checkout.AmountMinor / 100m || data.GetProperty("payment_currency").GetString() != checkout.Currency)
                    throw new DomainException("Cashfree charge does not match the subscribed plan.");
                if (occurredAt > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("Cashfree payment time is in the future.");
                var remote = await cashfree.GetSubscriptionAsync(subscriptionId, ct);
                if (remote.PlanId != checkout.ProviderPlanId) throw new DomainException("Cashfree plan mismatch.");
                var periodEnd = occurredAt.AddMonths(1);
                if (periodEnd > DateTimeOffset.UtcNow) await ActivatePaidPeriodAsync(checkout, periodEnd, ct);
            }
            else if (eventType == "SUBSCRIPTION_AUTH_STATUS")
            {
                var auth = data.GetProperty("authorization_details");
                if (auth.GetProperty("authorization_status").GetString() == "ACTIVE" && checkout.Status == "pending") checkout.Status = "authenticated";
            }
            else if (eventType == "SUBSCRIPTION_STATUS_CHANGED")
            {
                var status = details.GetProperty("subscription_status").GetString();
                if (status == "ACTIVE" && checkout.Status == "pending") checkout.Status = "authenticated";
                if (status is "CANCELLED" or "CUSTOMER_CANCELLED" or "EXPIRED" or "LINK_EXPIRED") checkout.Status = "cancelled";
            }
            else if (eventType == "SUBSCRIPTION_PAYMENT_FAILED" && checkout.Status == "authenticated") checkout.Status = "payment_pending";
            db.BillingWebhookEvents.Add(new BillingWebhookEvent { TenantId = checkout.TenantId, Provider = cashfree.ProviderKey, ProviderEventId = eventId, EventType = eventType });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { currentTenant.Clear(); }
    }

    private async Task ActivatePaidPeriodAsync(BillingCheckout checkout, DateTimeOffset periodEnd, CancellationToken ct)
    {
        var paid = await db.TenantSubscriptions.FirstOrDefaultAsync(x => x.BillingProvider == checkout.Provider && x.ProviderSubscriptionId == checkout.ProviderSubscriptionId, ct);
        if (paid is null)
        {
            paid = new TenantSubscription { TenantId = checkout.TenantId, BillingProvider = checkout.Provider, ProviderSubscriptionId = checkout.ProviderSubscriptionId };
            db.TenantSubscriptions.Add(paid);
        }
        if (paid.EndsAt.HasValue && periodEnd <= paid.EndsAt.Value) return;
        paid.PlanCode = checkout.PlanCode;
        paid.EmployeeLimit = checkout.EmployeeLimit;
        paid.StartsAt = DateTimeOffset.UtcNow;
        paid.EndsAt = periodEnd;
        paid.IsActive = true;
        checkout.CurrentPeriodEnd = periodEnd;
        checkout.Status = "active";
        var tenant = await db.Tenants.SingleAsync(x => x.Id == checkout.TenantId, ct);
        if (tenant.Status == TenantStatus.Trial) { tenant.Status = TenantStatus.Active; tenant.TrialEndsAt = null; }
        foreach (var old in await db.TenantSubscriptions.Where(x => x.Id != paid.Id && x.IsActive).ToListAsync(ct)) old.IsActive = false;
    }
}

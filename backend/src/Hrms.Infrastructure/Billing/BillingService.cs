using System.Text.Json;
using Hrms.Application;
using Hrms.Domain;
using Hrms.Domain.Common;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Billing;

public sealed class BillingService(HrmsDbContext db, ICurrentTenant currentTenant, ISubscriptionPaymentGateway gateway, IConfiguration configuration)
{
    public IReadOnlyList<BillingPlan> Plans()
    {
        return configuration.GetSection("Billing:Plans").GetChildren().Select(section =>
        {
            var providerPlanId = section[$"{(gateway.IsTest ? "Test" : "Live")}:{gateway.PlanConfigurationKey}"] ?? "";
            return new BillingPlan(section.Key, section["Name"] ?? section.Key,
                section["Currency"] ?? "INR", long.TryParse(section["AmountMinor"], out var amount) ? amount : 0,
                int.TryParse(section["EmployeeLimit"], out var limit) ? limit : 0, providerPlanId);
        }).Where(x => x.AmountMinor > 0 && x.EmployeeLimit > 0 && x.Currency == "INR" && !string.IsNullOrWhiteSpace(x.ProviderPlanId))
          .OrderBy(x => x.AmountMinor).ToArray();
    }

    public async Task<BillingStatus> StatusAsync(CancellationToken ct)
    {
        var subscription = await db.TenantSubscriptions.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var checkout = await db.BillingCheckouts.Where(x => x.Provider == gateway.ProviderKey).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var active = subscription is { IsActive: true } && subscription.StartsAt <= now && (!subscription.EndsAt.HasValue || subscription.EndsAt > now);
        return new BillingStatus(subscription?.PlanCode ?? "unassigned", active,
            subscription?.EndsAt, checkout?.Status == "pending" ? checkout.PlanCode : null,
            checkout?.Status,
            checkout?.Status == "pending" ? checkout.CheckoutUrl : null, gateway.IsTest);
    }

    public async Task<BillingCheckoutResult> StartAsync(string planCode, CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new UnauthorizedAccessException();
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct) ?? throw new KeyNotFoundException("Company not found.");
        if (tenant.Slug == "platform" || tenant.Status is TenantStatus.Suspended or TenantStatus.Cancelled)
            throw new UnauthorizedAccessException("This workspace cannot start a subscription.");
        var plan = Plans().FirstOrDefault(x => x.Code == planCode) ?? throw new DomainException("The selected plan is not configured.");
        if (!string.Equals(tenant.DefaultCurrency, plan.Currency, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("The company currency must match the billing plan currency.");
        var licensed = await db.Employees.CountAsync(x => x.Status == EmploymentStatus.Active || x.Status == EmploymentStatus.Probation || x.Status == EmploymentStatus.NoticePeriod, ct);
        if (plan.EmployeeLimit < licensed) throw new DomainException("This plan has fewer seats than the current licensed workforce.");
        if (await db.BillingCheckouts.AnyAsync(x => x.Provider == gateway.ProviderKey && (x.Status == "pending" || x.Status == "active" || x.Status == "payment_pending"), ct))
            throw new DomainException("A Razorpay subscription already exists for this company. Contact the platform administrator to change plans.");
        var result = await gateway.CreateSubscriptionAsync(plan, tenantId, ct);
        db.BillingCheckouts.Add(new BillingCheckout
        {
            TenantId = tenantId, Provider = gateway.ProviderKey, ProviderSubscriptionId = result.SubscriptionId,
            CheckoutUrl = result.CheckoutUrl,
            PlanCode = plan.Code, ProviderPlanId = plan.ProviderPlanId, Currency = plan.Currency,
            AmountMinor = plan.AmountMinor, EmployeeLimit = plan.EmployeeLimit, IsTest = gateway.IsTest
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
                var hrmsSubscription = await db.TenantSubscriptions.FirstOrDefaultAsync(x => x.BillingProvider == gateway.ProviderKey && x.ProviderSubscriptionId == providerSubscriptionId, ct);
                if (hrmsSubscription is null)
                {
                    hrmsSubscription = new TenantSubscription { TenantId = checkout.TenantId, BillingProvider = gateway.ProviderKey, ProviderSubscriptionId = providerSubscriptionId };
                    db.TenantSubscriptions.Add(hrmsSubscription);
                }
                if (!hrmsSubscription.EndsAt.HasValue || periodEnd > hrmsSubscription.EndsAt.Value)
                {
                    hrmsSubscription.PlanCode = checkout.PlanCode;
                    hrmsSubscription.EmployeeLimit = checkout.EmployeeLimit;
                    hrmsSubscription.StartsAt = now;
                    hrmsSubscription.EndsAt = periodEnd;
                    hrmsSubscription.IsActive = true;
                    checkout.CurrentPeriodEnd = periodEnd;
                    checkout.Status = "active";
                    var tenant = await db.Tenants.SingleAsync(x => x.Id == checkout.TenantId, ct);
                    if (tenant.Status == TenantStatus.Trial) { tenant.Status = TenantStatus.Active; tenant.TrialEndsAt = null; }
                    foreach (var old in await db.TenantSubscriptions.Where(x => x.Id != hrmsSubscription.Id && x.IsActive).ToListAsync(ct)) old.IsActive = false;
                }
            }
            else if (eventType is "subscription.cancelled" or "subscription.completed" or "subscription.halted")
            {
                checkout.Status = eventType["subscription.".Length..];
            }
            else if (eventType == "subscription.pending" && checkout.Status != "active") checkout.Status = "payment_pending";
            db.BillingWebhookEvents.Add(new BillingWebhookEvent { TenantId = checkout.TenantId, Provider = gateway.ProviderKey, ProviderEventId = eventId, EventType = eventType });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { currentTenant.Clear(); }
    }
}

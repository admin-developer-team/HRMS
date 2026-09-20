using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Billing;
using Hrms.Infrastructure.Identity;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Hrms.Tests;

public sealed class BillingFlowTests
{
    [Fact]
    public async Task Billing_status_shows_platform_access_override_instead_of_provider_period()
    {
        var tenantContext = new CurrentTenant();
        var overrideEnd = DateTimeOffset.UtcNow.AddHours(2);
        var tenant = new Tenant { Name = "Example", Slug = "example", Status = TenantStatus.Active,
            AdminAccessEnabled = true, AdminAccessStartsAt = DateTimeOffset.UtcNow.AddDays(-1), AdminAccessEndsAt = overrideEnd };
        tenantContext.Set(tenant.Id, tenant.Slug);
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantContext,
            new TestCurrentUser { IsPlatformAdmin = true }, new TestNotificationPublisher());
        db.Tenants.Add(tenant);
        db.TenantSubscriptions.Add(new TenantSubscription { TenantId = tenant.Id, PlanCode = "starter", StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAt = DateTimeOffset.UtcNow.AddMonths(1), IsActive = true, BillingProvider = "razorpay_live" });
        await db.SaveChangesAsync();
        var service = new BillingService(db, tenantContext, new FakeGateway(), new ConfigurationBuilder().Build());

        var status = await service.StatusAsync(default);

        Assert.True(status.Active);
        Assert.True(status.AdminManaged);
        Assert.Equal(overrideEnd, status.EndsAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstChargeStartsAtTrialEndOrImmediatelyWhenTrialAlreadyEnded(bool expired)
    {
        var tenantContext = new CurrentTenant();
        var tenant = new Tenant { Name = "Example", Slug = "example", Status = TenantStatus.Trial,
            DefaultCurrency = "INR", TrialEndsAt = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 30) };
        tenantContext.Set(tenant.Id, tenant.Slug);
        await using var db = new HrmsDbContext(new DbContextOptionsBuilder<HrmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantContext,
            new TestCurrentUser { IsPlatformAdmin = true }, new TestNotificationPublisher());
        db.Tenants.Add(tenant);
        db.TenantSubscriptions.Add(new TenantSubscription { TenantId = tenant.Id, PlanCode = "trial",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = tenant.TrialEndsAt, IsActive = true });
        await db.SaveChangesAsync();
        var gateway = new FakeGateway();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Billing:Plans:starter:Name"] = "Starter",
            ["Billing:Plans:starter:Currency"] = "INR",
            ["Billing:Plans:starter:AmountMinor"] = "1000",
            ["Billing:Plans:starter:EmployeeLimit"] = "50",
            ["Billing:Plans:starter:Live:RazorpayPlanId"] = "plan_12345678901234",
            ["Billing:Razorpay:Live:KeyId"] = "rzp_live_example",
            ["Billing:Razorpay:Live:KeySecret"] = "example-secret",
            ["Billing:Razorpay:Live:WebhookSecret"] = "example-webhook-secret"
        }).Build();
        var service = new BillingService(db, tenantContext, gateway, settings);

        var checkout = await service.StartAsync("starter", default);

        Assert.Equal("sub_12345678901234", checkout.SubscriptionId);
        Assert.Equal(expired ? null : tenant.TrialEndsAt, gateway.FirstChargeAt);
        Assert.Equal("pending", (await db.BillingCheckouts.SingleAsync()).Status);
        var resumed = await service.StartAsync("starter", default);
        Assert.Equal(checkout.SubscriptionId, resumed.SubscriptionId);
        Assert.Equal(checkout.CheckoutUrl, resumed.CheckoutUrl);
        Assert.Equal("rzp_live_example", resumed.PublicKeyId);
    }

    private sealed class FakeGateway : ISubscriptionPaymentGateway
    {
        public string ProviderKey => "razorpay_live";
        public string PlanConfigurationKey => "RazorpayPlanId";
        public bool IsTest => false;
        public DateTimeOffset? FirstChargeAt { get; private set; }
        public Task<BillingCheckoutResult> CreateSubscriptionAsync(BillingPlan plan, Guid tenantId, DateTimeOffset? firstChargeAt, CancellationToken ct)
        {
            Assert.Equal(1000, plan.AmountMinor);
            FirstChargeAt = firstChargeAt;
            return Task.FromResult(new BillingCheckoutResult("sub_12345678901234", "https://rzp.io/i/example"));
        }
        public Task<ProviderSubscriptionStatus> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
            Task.FromResult(new ProviderSubscriptionStatus("created", null, "plan_12345678901234", 0));
        public bool VerifyWebhook(ReadOnlySpan<byte> body, string signature) => false;
    }
}

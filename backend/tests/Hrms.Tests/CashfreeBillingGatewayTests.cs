using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hrms.Application;
using Hrms.Infrastructure.Billing;
using Microsoft.Extensions.Configuration;

namespace Hrms.Tests;

public sealed class CashfreeBillingGatewayTests
{
    private static IConfiguration Settings() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Billing:Cashfree:Mode"] = "test",
        ["Billing:Cashfree:Test:ClientId"] = "client-id",
        ["Billing:Cashfree:Test:ClientSecret"] = "client-secret",
        ["Billing:Cashfree:Test:WebhookSecret"] = "webhook-secret",
        ["Billing:Plans:starter:Test:CashfreePlanId"] = "starter_monthly_10"
    }).Build();

    [Fact]
    public void SignedWebhookRequiresRawBodyFreshTimestampAndCorrectSecret()
    {
        var gateway = new CashfreeSubscriptionGateway(new HttpClient(), Settings());
        var body = Encoding.UTF8.GetBytes("{\"type\":\"SUBSCRIPTION_PAYMENT_SUCCESS\"}");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes("webhook-secret"),
            Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray()));
        Assert.True(gateway.VerifyWebhook(body, timestamp, signature));
        Assert.False(gateway.VerifyWebhook(Encoding.UTF8.GetBytes("{}"), timestamp, signature));
        Assert.False(gateway.VerifyWebhook(body, (DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds()).ToString(), signature));
        Assert.False(gateway.VerifyWebhook(body, timestamp, "invalid"));
    }

    [Fact]
    public async Task CreatesMonthlyMandateWithFirstChargeAfterTrial()
    {
        var handler = new RecordingHandler();
        var gateway = new CashfreeSubscriptionGateway(new HttpClient(handler), Settings());
        var chargeAt = DateTimeOffset.UtcNow.AddDays(30);
        var result = await gateway.CreateSubscriptionAsync(new BillingPlan("starter", "Starter", "INR", 1000, 50, "starter_monthly_10", "cashfree"),
            Guid.NewGuid(), "Billing Admin", "admin@example.com", "9876543210", "https://example.com/api/v1/billing/cashfree-return", chargeAt, default);
        Assert.Equal("cashfree", result.Provider);
        Assert.True(result.TestMode);
        Assert.StartsWith("cashfree:session_", result.CheckoutUrl);
        using var body = JsonDocument.Parse(handler.Body);
        Assert.Equal("starter_monthly_10", body.RootElement.GetProperty("plan_details").GetProperty("plan_id").GetString());
        Assert.Equal("9876543210", body.RootElement.GetProperty("customer_details").GetProperty("customer_phone").GetString());
        Assert.Equal(chargeAt.ToString("O"), body.RootElement.GetProperty("subscription_first_charge_time").GetString());
        Assert.DoesNotContain("client-secret", result.CheckoutUrl);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("2026-01-01", request.Headers.GetValues("x-api-version").Single());
            if (request.Method == HttpMethod.Get)
            {
                if (request.RequestUri?.AbsolutePath.StartsWith("/pg/subscriptions/") == true)
                    return new(HttpStatusCode.NotFound);
                Assert.Equal("https://sandbox.cashfree.com/pg/plans/starter_monthly_10", request.RequestUri?.ToString());
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"plan_id\":\"starter_monthly_10\",\"plan_type\":\"PERIODIC\",\"plan_interval_type\":\"MONTH\",\"plan_intervals\":1,\"plan_currency\":\"INR\",\"plan_recurring_amount\":10,\"plan_max_amount\":10,\"plan_max_cycles\":120,\"plan_status\":\"ACTIVE\"}") };
            }
            Body = await request.Content!.ReadAsStringAsync(ct);
            using var body = JsonDocument.Parse(Body);
            var id = body.RootElement.GetProperty("subscription_id").GetString();
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            {
                subscription_id = id, subscription_session_id = "session_123",
                plan_details = new { plan_id = "starter_monthly_10" }
            })) };
        }
    }
}

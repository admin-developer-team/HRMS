using System.Net;
using System.Security.Cryptography;
using System.Text;
using Hrms.Application;
using Hrms.Infrastructure.Billing;
using Microsoft.Extensions.Configuration;

namespace Hrms.Tests;

public sealed class BillingGatewayTests
{
    private static IConfiguration Settings(string mode = "test") => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Billing:Razorpay:Mode"] = mode,
            ["Billing:Razorpay:Test:KeyId"] = "rzp_test_1234567890",
            ["Billing:Razorpay:Test:KeySecret"] = "test-secret",
            ["Billing:Razorpay:Test:WebhookSecret"] = "test-webhook-secret",
            ["Billing:Razorpay:Live:KeyId"] = "rzp_live_1234567890",
            ["Billing:Razorpay:Live:KeySecret"] = "live-secret",
            ["Billing:Razorpay:Live:WebhookSecret"] = "live-webhook-secret"
        }).Build();

    [Fact]
    public void WebhookSignatureUsesRawBodyAndSelectedMode()
    {
        var body = Encoding.UTF8.GetBytes("{\"event\":\"subscription.charged\"}");
        var testSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-webhook-secret"), body));
        var test = new RazorpaySubscriptionGateway(new HttpClient(), Settings());
        var live = new RazorpaySubscriptionGateway(new HttpClient(), Settings("live"));
        Assert.True(test.VerifyWebhook(body, testSignature));
        Assert.False(live.VerifyWebhook(body, testSignature));
        Assert.False(test.VerifyWebhook(Encoding.UTF8.GetBytes("{}"), testSignature));
        Assert.False(test.VerifyWebhook(body, "not-a-signature"));
        Assert.Equal("razorpay_test", test.ProviderKey);
        Assert.Equal("razorpay_live", live.ProviderKey);
    }

    [Fact]
    public async Task CreatesHostedSubscriptionWithoutExposingSecretInResult()
    {
        var handler = new RecordingHandler();
        var gateway = new RazorpaySubscriptionGateway(new HttpClient(handler), Settings());
        var result = await gateway.CreateSubscriptionAsync(new BillingPlan("starter", "Starter", "INR", 10000, 50, "plan_12345678901234"), Guid.NewGuid(), default);
        Assert.Equal("sub_12345678901234", result.SubscriptionId);
        Assert.Equal("https://rzp.io/rzp/example", result.CheckoutUrl);
        Assert.Contains("plan_12345678901234", handler.Body);
        Assert.Equal("Basic", handler.AuthorizationScheme);
        Assert.DoesNotContain("test-secret", result.CheckoutUrl);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string? AuthorizationScheme { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("https://api.razorpay.com/v1/plans/plan_12345678901234", request.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"plan_12345678901234\",\"period\":\"monthly\",\"interval\":1,\"item\":{\"amount\":10000,\"currency\":\"INR\"}}") };
            }
            Assert.Equal("https://api.razorpay.com/v1/subscriptions", request.RequestUri?.ToString());
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"sub_12345678901234\",\"short_url\":\"https://rzp.io/rzp/example\"}") };
        }
    }
}

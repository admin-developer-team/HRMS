using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hrms.Application;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Billing;

public sealed class RazorpaySubscriptionGateway(HttpClient client, IConfiguration configuration) : ISubscriptionPaymentGateway
{
    private string Mode => configuration["Billing:Razorpay:Mode"]?.Trim().ToLowerInvariant() ?? "live";
    public bool IsTest => Mode == "test";
    public string ProviderKey => IsTest ? "razorpay_test" : "razorpay_live";
    public string PlanConfigurationKey => "RazorpayPlanId";

    private (string Id, string Secret, string WebhookSecret) Credentials()
    {
        if (Mode is not ("test" or "live")) throw new InvalidOperationException("Billing:Razorpay:Mode must be test or live.");
        var section = configuration.GetSection($"Billing:Razorpay:{(IsTest ? "Test" : "Live")}");
        var id = section["KeyId"] ?? "";
        var secret = section["KeySecret"] ?? "";
        var webhookSecret = section["WebhookSecret"] ?? "";
        if (!id.StartsWith(IsTest ? "rzp_test_" : "rzp_live_", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(webhookSecret))
            throw new InvalidOperationException("Configure the Razorpay key ID, key secret and separate webhook secret for the selected mode.");
        return (id, secret, webhookSecret);
    }

    public async Task<BillingCheckoutResult> CreateSubscriptionAsync(BillingPlan plan, Guid tenantId, DateTimeOffset? firstChargeAt, CancellationToken ct)
    {
        var (id, secret, _) = Credentials();
        using (var planRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.razorpay.com/v1/plans/{Uri.EscapeDataString(plan.ProviderPlanId)}"))
        {
            planRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")));
            using var planResponse = await client.SendAsync(planRequest, ct);
            if (!planResponse.IsSuccessStatusCode) throw new InvalidOperationException("Could not load the configured Razorpay plan in the selected mode.");
            using var planJson = JsonDocument.Parse(await planResponse.Content.ReadAsStringAsync(ct));
            var remote = planJson.RootElement;
            var item = remote.GetProperty("item");
            if (remote.GetProperty("id").GetString() != plan.ProviderPlanId
                || remote.GetProperty("period").GetString() != "monthly"
                || remote.GetProperty("interval").GetInt32() != 1
                || item.GetProperty("amount").GetInt64() != plan.AmountMinor
                || item.GetProperty("currency").GetString() != plan.Currency)
                throw new InvalidOperationException("The Razorpay plan must be monthly and match the configured INR price.");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")));
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            plan_id = plan.ProviderPlanId,
            total_count = 120,
            quantity = 1,
            customer_notify = true,
            start_at = firstChargeAt?.ToUnixTimeSeconds(),
            notes = new { tenant_id = tenantId.ToString(), plan_code = plan.Code }
        }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Razorpay subscription creation failed ({(int)response.StatusCode}). Check plan ID and Subscriptions access in the selected mode.");
        using var json = JsonDocument.Parse(content);
        var root = json.RootElement;
        var subscriptionId = root.GetProperty("id").GetString();
        var url = root.GetProperty("short_url").GetString();
        if (string.IsNullOrWhiteSpace(subscriptionId) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !(uri.Host == "rzp.io" || uri.Host.EndsWith(".razorpay.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Razorpay returned an invalid subscription checkout URL.");
        return new BillingCheckoutResult(subscriptionId, url!, "razorpay", IsTest, id);
    }

    public async Task<ProviderSubscriptionStatus> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId) || !subscriptionId.StartsWith("sub_", StringComparison.Ordinal))
            throw new ArgumentException("Invalid subscription ID.", nameof(subscriptionId));
        var (id, secret, _) = Credentials();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.razorpay.com/v1/subscriptions/{Uri.EscapeDataString(subscriptionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")));
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Could not verify the Razorpay subscription status.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (root.GetProperty("id").GetString() != subscriptionId) throw new InvalidOperationException("Razorpay subscription ID mismatch.");
        DateTimeOffset? end = root.TryGetProperty("current_end", out var value) && value.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(value.GetInt64()) : null;
        return new ProviderSubscriptionStatus(root.GetProperty("status").GetString() ?? "unknown", end,
            root.GetProperty("plan_id").GetString() ?? "", root.TryGetProperty("paid_count", out var count) ? count.GetInt32() : 0);
    }

    public bool VerifyWebhook(ReadOnlySpan<byte> body, string signature)
    {
        var (_, _, secret) = Credentials();
        if (signature.Length != 64 || !signature.All(Uri.IsHexDigit)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        var supplied = Convert.FromHexString(signature);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

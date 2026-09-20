using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hrms.Application;
using Microsoft.Extensions.Configuration;

namespace Hrms.Infrastructure.Billing;

public sealed class CashfreeSubscriptionGateway(HttpClient client, IConfiguration configuration)
{
    private string Mode => configuration["Billing:Cashfree:Mode"]?.Trim().ToLowerInvariant() ?? "live";
    public bool IsTest => Mode == "test";
    public string ProviderKey => IsTest ? "cashfree_test" : "cashfree_live";
    public string? PlanId => configuration[$"Billing:Plans:starter:{(IsTest ? "Test" : "Live")}:CashfreePlanId"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PlanId)
        && !string.IsNullOrWhiteSpace(configuration[$"Billing:Cashfree:{(IsTest ? "Test" : "Live")}:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration[$"Billing:Cashfree:{(IsTest ? "Test" : "Live")}:ClientSecret"])
        && !string.IsNullOrWhiteSpace(configuration[$"Billing:Cashfree:{(IsTest ? "Test" : "Live")}:WebhookSecret"]);
    private string BaseUrl => IsTest ? "https://sandbox.cashfree.com" : "https://api.cashfree.com";

    private (string Id, string Secret, string WebhookSecret) Credentials()
    {
        if (Mode is not ("test" or "live")) throw new InvalidOperationException("Billing:Cashfree:Mode must be test or live.");
        var section = configuration.GetSection($"Billing:Cashfree:{(IsTest ? "Test" : "Live")}");
        var id = section["ClientId"] ?? "";
        var secret = section["ClientSecret"] ?? "";
        var webhookSecret = section["WebhookSecret"] ?? "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(webhookSecret))
            throw new InvalidOperationException("Configure Cashfree client ID, client secret and webhook secret for the selected mode.");
        return (id, secret, webhookSecret);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var (id, secret, _) = Credentials();
        var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Add("x-api-version", "2026-01-01");
        request.Headers.Add("x-client-id", id);
        request.Headers.Add("x-client-secret", secret);
        return request;
    }

    public async Task<BillingCheckoutResult> CreateSubscriptionAsync(BillingPlan plan, Guid tenantId, string customerName, string customerEmail,
        string phone, string returnUrl, DateTimeOffset? firstChargeAt, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, "^[6-9][0-9]{9}$"))
            throw new Hrms.Domain.Common.DomainException("Enter a valid 10-digit Indian mobile number for Cashfree mandate authorization.");
        using (var planRequest = Request(HttpMethod.Get, $"/pg/plans/{Uri.EscapeDataString(plan.ProviderPlanId)}"))
        using (var planResponse = await client.SendAsync(planRequest, ct))
        {
            if (!planResponse.IsSuccessStatusCode) throw new InvalidOperationException("Could not load the configured Cashfree plan.");
            using var planJson = JsonDocument.Parse(await planResponse.Content.ReadAsStringAsync(ct));
            var remote = planJson.RootElement;
            if (remote.GetProperty("plan_id").GetString() != plan.ProviderPlanId
                || remote.GetProperty("plan_type").GetString() != "PERIODIC"
                || remote.GetProperty("plan_interval_type").GetString() != "MONTH"
                || remote.GetProperty("plan_intervals").GetInt32() != 1
                || remote.GetProperty("plan_currency").GetString() != plan.Currency
                || remote.GetProperty("plan_recurring_amount").GetDecimal() != plan.AmountMinor / 100m
                || remote.GetProperty("plan_max_amount").GetDecimal() < plan.AmountMinor / 100m
                || remote.GetProperty("plan_max_cycles").GetInt32() < 120
                || remote.GetProperty("plan_status").GetString() != "ACTIVE")
                throw new InvalidOperationException("Cashfree plan must be active, monthly, ₹10, and allow at least 120 cycles.");
        }
        // A stable merchant ID lets a retry recover a mandate if the provider replied but our request timed out.
        var subscriptionId = $"hrms_{tenantId:N}_starter";
        using (var existingRequest = Request(HttpMethod.Get, $"/pg/subscriptions/{subscriptionId}"))
        using (var existingResponse = await client.SendAsync(existingRequest, ct))
        {
            if (existingResponse.IsSuccessStatusCode)
            {
                using var existingJson = JsonDocument.Parse(await existingResponse.Content.ReadAsStringAsync(ct));
                var existing = existingJson.RootElement;
                var status = existing.GetProperty("subscription_status").GetString();
                if (existing.GetProperty("subscription_id").GetString() != subscriptionId
                    || existing.GetProperty("plan_details").GetProperty("plan_id").GetString() != plan.ProviderPlanId)
                    throw new InvalidOperationException("Existing Cashfree mandate does not match this plan.");
                if (status is "INITIALIZED" or "ACTIVE" or "BANK_APPROVAL_PENDING")
                {
                    var existingSession = existing.GetProperty("subscription_session_id").GetString();
                    if (!string.IsNullOrWhiteSpace(existingSession))
                        return new BillingCheckoutResult(subscriptionId, "cashfree:" + existingSession, "cashfree", IsTest);
                }
                throw new InvalidOperationException("An earlier Cashfree mandate exists for this company. Contact support before starting a new one.");
            }
            if (existingResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("Could not check for an existing Cashfree subscription.");
        }
        using var request = Request(HttpMethod.Post, "/pg/subscriptions");
        request.Headers.Add("x-idempotency-key", tenantId.ToString());
        var chargeAt = firstChargeAt ?? DateTimeOffset.UtcNow.AddMinutes(10);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            subscription_id = subscriptionId,
            customer_details = new { customer_name = customerName, customer_email = customerEmail, customer_phone = phone },
            plan_details = new { plan_id = plan.ProviderPlanId },
            authorization_details = new { authorization_amount = 1, authorization_amount_refund = true, payment_methods = new[] { "upi", "card" } },
            subscription_meta = new { return_url = returnUrl, notification_channel = new[] { "EMAIL" } },
            subscription_first_charge_time = chargeAt.ToString("O", CultureInfo.InvariantCulture),
            subscription_expiry_time = chargeAt.AddYears(11).ToString("O", CultureInfo.InvariantCulture),
            subscription_tags = new { tenant_id = tenantId.ToString(), plan_code = plan.Code }
        }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Cashfree subscription creation failed ({(int)response.StatusCode}). Check Subscriptions access and plan configuration.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        var session = root.GetProperty("subscription_session_id").GetString();
        if (root.GetProperty("subscription_id").GetString() != subscriptionId || string.IsNullOrWhiteSpace(session)
            || root.GetProperty("plan_details").GetProperty("plan_id").GetString() != plan.ProviderPlanId)
            throw new InvalidOperationException("Cashfree returned inconsistent subscription details.");
        return new BillingCheckoutResult(subscriptionId, "cashfree:" + session, "cashfree", IsTest);
    }

    public async Task<ProviderSubscriptionStatus> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, $"/pg/subscriptions/{Uri.EscapeDataString(subscriptionId)}");
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Could not verify the Cashfree subscription status.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (root.GetProperty("subscription_id").GetString() != subscriptionId) throw new InvalidOperationException("Cashfree subscription ID mismatch.");
        // ACTIVE is authorization only; paid access is granted solely by a verified debit webhook.
        return new ProviderSubscriptionStatus(root.GetProperty("subscription_status").GetString()?.ToLowerInvariant() ?? "unknown", null,
            root.GetProperty("plan_details").GetProperty("plan_id").GetString() ?? "", 0);
    }

    public bool VerifyWebhook(ReadOnlySpan<byte> body, string timestamp, string signature)
    {
        var (_, _, secret) = Credentials();
        if (!long.TryParse(timestamp, out var seconds) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300) return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(signature); }
        catch (FormatException) { return false; }
        var prefix = Encoding.UTF8.GetBytes(timestamp);
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

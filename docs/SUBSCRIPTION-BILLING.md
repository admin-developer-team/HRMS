# Subscription billing setup

The public signup offers one Starter plan: **₹10 per month for up to 50 employees**. Account activation starts a 30-day trial. The company administrator must authorize a recurring mandate through either Razorpay or Cashfree before the trial workspace opens. The first ₹10 plan charge is scheduled for the trial end; the provider may make a small refundable authorization transaction at signup. Only a verified recurring charge grants paid access. Employees see an access-paused page when billing needs attention; company administrators can reach billing. A company can have only one open mandate; changing provider requires closing the old mandate first.

## Razorpay dashboard

1. Complete Razorpay business activation and ask Razorpay to enable **Subscriptions** and the recurring payment methods you intend to accept in Live Mode. Confirm supported payment methods and mandate limits with Razorpay.
2. In **Live Mode → Subscriptions → Plans**, create an **INR 10.00**, **monthly**, **interval 1** plan. Copy its `plan_...` ID. The API checks amount, currency and interval before creating a mandate.
3. Generate a Live Mode API key. In **Live Mode → Account & Settings → Webhooks**, create a separate random webhook secret and register `https://hrms.avntechnologies.co.in/api/v1/billing/webhooks/razorpay` (replace with the actual public API host). Select `subscription.authenticated`, `subscription.charged`, `subscription.pending`, `subscription.halted`, `subscription.cancelled`, and `subscription.completed`. Set a webhook failure alert email. The endpoint must be reachable publicly over HTTPS.

The Razorpay API key secret and webhook secret are different. Save both only in server environment variables. Do not add them to Angular, Git, or the Razorpay plan configuration returned to the browser.

## Cashfree dashboard

1. Activate the Cashfree merchant account and request **Subscriptions** for live recurring payments. Confirm UPI AutoPay or card mandate access in your account. Cashfree's [subscription API](https://www.cashfree.com/docs/api-reference/payments/latest/subscription/create-subscription) and [checkout demo](https://www.cashfree.com/devstudio/preview/subs/web/checkout) describe the hosted flow.
2. Create an **ACTIVE PERIODIC** plan for **INR 10.00**, **MONTH**, interval **1**, maximum charge at least ₹10, and at least **120 cycles**. Copy its Cashfree plan ID. The API fetches the plan and checks those settings before creating a mandate.
3. In the Cashfree dashboard, register a Subscriptions webhook at `https://hrms.avntechnologies.co.in/api/v1/billing/webhooks/cashfree` (replace with the actual public API host). Select `SUBSCRIPTION_AUTH_STATUS`, `SUBSCRIPTION_STATUS_CHANGED`, `SUBSCRIPTION_PAYMENT_SUCCESS`, and `SUBSCRIPTION_PAYMENT_FAILED` using the 2026-01-01 webhook version. Save its webhook signing secret. The endpoint must be publicly reachable over HTTPS. Cashfree's [event formats](https://www.cashfree.com/docs/api-reference/payments/latest/subscription/webhooks) and [signature instructions](https://www.cashfree.com/docs/api-reference/payments/latest/subscription/webhook-signature) are the reference.

Cashfree asks for the billing administrator's 10-digit Indian mobile number when starting a mandate. It is sent to Cashfree for that checkout and is not stored in HRMS.

## Server configuration

Set these environment variables on the **API server** (values are examples, not credentials):

```text
Billing__Razorpay__Mode=live
Billing__Razorpay__Live__KeyId=rzp_live_YOUR_KEY_ID
Billing__Razorpay__Live__KeySecret=YOUR_LIVE_KEY_SECRET
Billing__Razorpay__Live__WebhookSecret=YOUR_SEPARATE_LIVE_WEBHOOK_SECRET
Billing__Plans__starter__Live__RazorpayPlanId=plan_YOUR_LIVE_10_RUPEE_PLAN_ID
Billing__Cashfree__Mode=live
Billing__Cashfree__Live__ClientId=YOUR_LIVE_CLIENT_ID
Billing__Cashfree__Live__ClientSecret=YOUR_LIVE_CLIENT_SECRET
Billing__Cashfree__Live__WebhookSecret=YOUR_LIVE_WEBHOOK_SECRET
Billing__Plans__starter__Live__CashfreePlanId=YOUR_LIVE_10_RUPEE_PLAN_ID
```

Configure either provider or both. Starter's name, currency, ₹10 amount (`AmountMinor=1000`), and 50-employee limit are in `backend/src/Hrms.Api/appsettings.json`. Restart the API after setting its private environment. The billing page lists configured providers. The Cashfree return URL is built from `Tenancy:BaseDomain`; for local development `Billing:Cashfree:ReturnBaseUrl` is `http://localhost:4200`.

Test Mode requires separate test keys, webhook secret, and plan ID for each provider. Do not carry test subscription records into a live database; test and live objects cannot be interchanged.

For a Cashfree sandbox run, set `Billing__Cashfree__Mode=test` and the `Billing__Cashfree__Test__ClientId`, `ClientSecret`, and `WebhookSecret` variables, plus `Billing__Plans__starter__Test__CashfreePlanId`. The local return origin is `http://localhost:4200`; in any other non-production environment set `Billing__Cashfree__ReturnBaseUrl` to the actual public application origin. Cashfree requires that origin to be allowlisted for its checkout SDK.

## Flow

Create a fresh company at `/get-started`, activate the emailed administrator account, and sign in. The administrator is directed to billing to authorize automatic payment. Complete the provider checkout, then reopen billing and select **Refresh payment status**. The server checks the provider's mandate status; it does not trust a browser success message. A signed Razorpay `subscription.charged` or Cashfree `SUBSCRIPTION_PAYMENT_SUCCESS` recurring debit creates or extends paid access. Cashfree authorization payments are excluded. Webhook event IDs make retries idempotent. Razorpay can also reconcile a confirmed paid period directly from the provider if a webhook was missed. A failed charge does not grant a new period. Monitor Cashfree webhook delivery because its mandate status alone does not prove a recurring debit succeeded.

To test an immediate **real ₹10 plan charge**, create another fresh company, activate it, and use the platform administrator's **Company subscriptions** page to set **Trial ends** in the past **before** its administrator authorizes a provider. The first charge is scheduled shortly after authorization for Cashfree, or immediately after authorization for Razorpay. Verify the recurring charge and webhook in the provider dashboard and confirm that the company becomes active. For a genuine 30-day trial, authorize with a future trial end. **Changing the HRMS trial date after authorization does not move the provider's scheduled first charge.** Test both roles after expiry: administrators reach billing; employees see an access-paused page.

Razorpay creates 120 monthly cycles; Cashfree follows the cycle limit configured on its plan. Changing the Starter price later does not silently change an existing customer's mandate. Plan changes, cancellation, and payment-method changes are not yet self-service. To switch providers, cancel the old mandate in its dashboard, refresh billing status, and authorize the other provider. The new provider schedules its first charge when the existing paid period ends. Monitor webhook failures and reconcile provider charges against HRMS subscriptions before billing production customers at scale.

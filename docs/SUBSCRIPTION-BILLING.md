# Subscription billing setup

The HRMS supports Razorpay hosted recurring subscriptions. `ISubscriptionPaymentGateway` is the provider boundary; a later gateway can implement it without changing the tenant billing endpoints. Razorpay Test and Live use separate credentials and plan IDs. Use a separate database or reset test billing data before running Live against an existing database.

## Razorpay dashboard

1. Enable **Subscriptions** in the selected Razorpay mode. The Razorpay Subscriptions API returns an error if this product is not enabled for the account.
2. Create monthly INR plans in **Test Mode**. Record each `plan_...` ID. Make matching plans in **Live Mode** only when ready to charge real customers; plan IDs differ between modes.
3. In **Test Mode → Account & Settings → Webhooks**, create a separate random webhook secret and register `https://hrms.avntechnologies.co.in/api/v1/billing/webhooks/razorpay`. Subscribe to `subscription.charged`, `subscription.pending`, `subscription.halted`, `subscription.cancelled`, and `subscription.completed`. The webhook endpoint must be publicly reachable. Use a different webhook secret and endpoint registration in Live Mode.

The Razorpay API key secret and webhook secret are different. Save both only in server environment variables. Do not add them to Angular, Git, or the Razorpay plan configuration returned to the browser.

## Server configuration

Set these environment variables on the **API server** (values are examples, not credentials):

```text
Billing__Razorpay__Mode=test
Billing__Razorpay__Test__KeyId=rzp_test_YOUR_KEY_ID
Billing__Razorpay__Test__KeySecret=YOUR_TEST_KEY_SECRET
Billing__Razorpay__Test__WebhookSecret=YOUR_SEPARATE_TEST_WEBHOOK_SECRET
Billing__Plans__starter__Name=Starter
Billing__Plans__starter__Currency=INR
Billing__Plans__starter__AmountMinor=99900
Billing__Plans__starter__EmployeeLimit=50
Billing__Plans__starter__Test__RazorpayPlanId=plan_YOUR_TEST_PLAN_ID
```

`AmountMinor=99900` means ₹999.00. It must match the amount and monthly interval of the Razorpay plan. Add `professional` and `enterprise` using the same shape. Companies must use INR to buy these plans. The API exposes only configured plans.

For Live Mode, provide `Billing__Razorpay__Live__KeyId`, `KeySecret`, and `WebhookSecret`, each plan's `Live__RazorpayPlanId`, and set `Billing__Razorpay__Mode=live`. The key ID must begin with `rzp_live_`. The app refuses mismatched mode/key IDs. Keep test and live databases separate so test payments never grant production access.

## Flow

Tenant administrators open **Subscription & billing**, select a plan, and follow the Razorpay-hosted checkout URL. A signed `subscription.charged` webhook creates or extends paid access. Webhook event IDs are recorded to avoid processing retries twice. `subscription.pending` and `subscription.halted` do not revoke a paid period early; access ends on the paid-through date. Expired companies allow tenant administrators to sign in to the billing API only, so they can renew. Platform administrators can still manage company subscriptions manually as an override.

Plan changes and cancellation of an active recurring mandate are not yet self-service; use the Razorpay dashboard and platform administration for those operations. Do not create another mandate for a company that already has one.

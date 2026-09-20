import { DecimalPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';

interface BillingPlan { code: string; name: string; currency: string; amountMinor: number; employeeLimit: number; providerPlanId: string; provider: 'razorpay' | 'cashfree' }
interface BillingStatus { planCode: string; active: boolean; endsAt: string | null; pendingPlanCode: string | null; pendingStatus: string | null; pendingCheckoutUrl: string | null; testMode: boolean; trialEndsAt: string | null; pendingProvider: string | null; pendingSubscriptionId: string | null; razorpayKeyId: string | null; adminManaged: boolean; currentAmountMinor: number | null; currentCurrency: string | null }
interface Checkout { subscriptionId: string; checkoutUrl: string; provider: string; testMode: boolean; publicKeyId: string | null }

@Component({
  selector: 'app-billing-page',
  imports: [DatePipe, DecimalPipe],
  template: `
    <main class="billing-page">
      <header><span class="eyebrow">Company administration</span><h1>Subscription & billing</h1><p>Available plan prices are shown below. For a new subscription, the first plan payment is due after the trial. Razorpay may show a separate refundable mandate authorization now.</p></header>
      @if (!auth.user()?.roles?.includes('TENANT_ADMIN')) {
        <p class="notice">Only the company administrator can manage billing.</p>
      } @else {
        @if (status(); as current) {
          <section class="current"><h2>Current subscription</h2><p><strong>{{ current.planCode }}</strong> · {{ current.active ? 'Enabled' : 'Inactive' }}</p>
            @if (current.currentAmountMinor !== null) { <p>Existing {{ current.pendingProvider }} mandate: {{ current.currentCurrency }} {{ current.currentAmountMinor / 100 | number:'1.2-2' }} per billing cycle</p> }
            @if (current.trialEndsAt) { <p>Your free trial ends {{ current.trialEndsAt | date:'medium' }}. The first ₹10 payment is scheduled then after you authorize automatic billing.</p> }
            @else if (current.endsAt) { <p>{{ current.adminManaged ? 'Platform-admin access through' : 'Paid access through' }} {{ current.endsAt | date:'mediumDate' }}</p> }
            @if (current.pendingStatus === 'pending') { <p>Complete the {{ current.pendingProvider }} authorization to use the trial.</p> }
            @if (current.pendingStatus === 'authenticated') { <p>{{ trialExpired(current.trialEndsAt) ? 'Automatic billing is authorized. Waiting for the first confirmed ₹10 payment.' : 'Automatic billing is authorized. Your trial is ready.' }}</p> }
            @if (current.pendingStatus === 'payment_pending') { <p>A payment is processing. Access will update after a confirmed charge.</p> }
            @if (current.pendingCheckoutUrl) { <button type="button" (click)="resumeCheckout(current)">Continue authorization</button> }
            @if (current.testMode) { <p class="notice">Payment provider test mode: no real money is charged.</p> }
            <button type="button" (click)="loadStatus()">Refresh payment status</button>
          </section>
        }
        @if (error()) { <p class="error">{{ error() }}</p> }
        <section class="plans" aria-label="Available subscription plans">
          @for (plan of plans(); track plan.provider + ':' + plan.code) {
            <article><h2>{{ plan.name }} · {{ plan.provider === 'cashfree' ? 'Cashfree' : 'Razorpay' }}</h2><p class="price">₹{{ plan.amountMinor / 100 | number:'1.2-2' }} <small>/ billing cycle</small></p>
              <p>Up to {{ plan.employeeLimit }} employees</p>
              @if (plan.provider === 'cashfree') { <label for="cashfree-phone">Billing mobile number</label><input id="cashfree-phone" type="tel" inputmode="numeric" autocomplete="tel-national" maxlength="10" [value]="phone()" (input)="phone.set($any($event.target).value)" placeholder="10-digit Indian mobile number" /> }
              <button type="button" [disabled]="busy() || status()?.pendingStatus === 'authenticated' || status()?.pendingStatus === 'active' || status()?.pendingStatus === 'payment_pending' || (status()?.pendingStatus === 'pending' && status()?.pendingProvider !== plan.provider)" (click)="checkout(plan)">{{ planActionLabel(plan) }}</button>
            </article>
          } @empty { <p>No billing plans are configured yet. Ask the platform administrator to configure Razorpay or Cashfree.</p> }
        </section>
      }
    </main>
  `,
  styles: [`
    .billing-page{max-width:1100px;margin:0 auto;padding:36px 24px 70px}h1{font-size:2rem;margin:8px 0}header p{color:#64748b}.eyebrow{color:#2563eb;text-transform:uppercase;font-size:.8rem;font-weight:700;letter-spacing:.1em}
    .current,.plans article{background:#fff;border:1px solid #e2e8f0;border-radius:18px;padding:24px;box-shadow:0 8px 24px #0f172a0a}.current{margin:24px 0}.current h2,.plans h2{margin-top:0}.plans{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:18px}.price{font-size:1.5rem;font-weight:700}.price small{font-size:.85rem;font-weight:400;color:#64748b}
    button{border:0;border-radius:9px;background:#2563eb;color:white;padding:12px 18px;cursor:pointer;font-weight:700}button:disabled{opacity:.5;cursor:not-allowed}.notice{color:#7c3aed}.error{color:#b91c1c}label{display:block;margin:12px 0 5px;font-weight:600}input{padding:11px;border:1px solid #cbd5e1;border-radius:8px;width:100%;box-sizing:border-box;margin-bottom:12px}
  `],
})
export class BillingPage {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  readonly plans = signal<BillingPlan[]>([]);
  readonly status = signal<BillingStatus | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly phone = signal('');

  trialExpired(value: string | null): boolean { return !!value && new Date(value).getTime() <= Date.now(); }

  planActionLabel(plan: BillingPlan): string {
    const current = this.status();
    if (current?.pendingStatus === 'pending')
      return current.pendingProvider === plan.provider ? 'Continue authorization' : 'Another provider in progress';
    if (current?.pendingStatus === 'authenticated' || current?.pendingStatus === 'active')
      return 'Automatic billing authorized';
    if (current?.pendingStatus === 'payment_pending') return 'Payment processing';
    return 'Authorize automatic billing';
  }

  constructor() {
    if (!this.auth.user()?.roles.includes('TENANT_ADMIN')) return;
    this.api.get<BillingPlan[]>('/billing/plans').subscribe({ next: value => this.plans.set(value), error: () => this.error.set('Could not load billing plans.') });
    this.loadStatus();
  }

  loadStatus(): void {
    this.api.get<BillingStatus>('/billing/status').subscribe({
      next: value => {
        this.status.set(value);
        if (this.auth.session()?.billingOnly && (value.pendingStatus === 'authenticated' || (value.active && value.planCode !== 'trial'))) {
          this.auth.refreshSession().subscribe({ next: () => void this.router.navigate(['/dashboard']), error: () => undefined });
        }
      },
      error: () => this.error.set('Could not load subscription status.'),
    });
  }

  checkout(plan: BillingPlan): void {
    if (this.busy()) return;
    if (this.status()?.pendingStatus === 'pending' && this.status()?.pendingProvider === plan.provider && this.status()?.pendingCheckoutUrl) {
      void this.resumeCheckout(this.status()!);
      return;
    }
    if (plan.provider === 'cashfree' && !/^[6-9][0-9]{9}$/.test(this.phone())) { this.error.set('Enter a valid 10-digit Indian mobile number for Cashfree.'); return; }
    this.busy.set(true);
    this.error.set('');
    this.api.post<Checkout>('/billing/checkout', { planCode: plan.code, provider: plan.provider, customerPhone: plan.provider === 'cashfree' ? this.phone() : null }).subscribe({
      next: result => {
        if (result.provider === 'razorpay') void this.openRazorpay(result.subscriptionId, result.publicKeyId);
        else void this.openCheckout(result.checkoutUrl, result.testMode);
      },
      error: err => { this.busy.set(false); this.error.set(err.error?.detail ?? 'Could not create the subscription.'); this.toast.error(this.error()); },
    });
  }

  resumeCheckout(current: BillingStatus): void {
    if (current.pendingProvider === 'razorpay' && current.pendingSubscriptionId && current.razorpayKeyId)
      void this.openRazorpay(current.pendingSubscriptionId, current.razorpayKeyId);
    else if (current.pendingCheckoutUrl) void this.openCheckout(current.pendingCheckoutUrl, current.testMode);
  }

  async openRazorpay(subscriptionId: string, keyId: string | null): Promise<void> {
    try {
      if (!keyId?.startsWith('rzp_') || !subscriptionId.startsWith('sub_')) throw new Error('Razorpay checkout details are missing.');
      if (!(window as any).Razorpay) {
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement('script');
          script.src = 'https://checkout.razorpay.com/v1/checkout.js';
          script.onload = () => resolve();
          script.onerror = () => reject(new Error('Razorpay checkout could not load.'));
          document.head.appendChild(script);
        });
      }
      const checkout = new (window as any).Razorpay({
        key: keyId,
        subscription_id: subscriptionId,
        name: 'PeopleFlow HRMS',
        description: 'Starter automatic billing authorization',
        handler: (response: { razorpay_subscription_id?: string }) => {
          this.busy.set(false);
          if (response.razorpay_subscription_id !== subscriptionId) {
            this.error.set('Razorpay returned a different subscription. Refresh payment status.');
            return;
          }
          this.loadStatus();
          window.setTimeout(() => this.loadStatus(), 2000);
        },
        modal: { ondismiss: () => this.busy.set(false) },
      });
      checkout.open();
    } catch (error) {
      this.busy.set(false);
      this.error.set(error instanceof Error ? error.message : 'Could not open Razorpay checkout.');
      this.toast.error(this.error());
    }
  }

  async openCheckout(url: string, testMode: boolean): Promise<void> {
    if (!url.startsWith('cashfree:')) { window.location.assign(url); return; }
    try {
      const session = url.slice('cashfree:'.length);
      if (!session) throw new Error('Missing Cashfree session.');
      if (!(window as any).Cashfree) {
        await new Promise<void>((resolve, reject) => {
          const script = document.createElement('script');
          script.src = 'https://sdk.cashfree.com/js/v3/cashfree.js';
          script.onload = () => resolve();
          script.onerror = () => reject(new Error('Cashfree checkout could not load.'));
          document.head.appendChild(script);
        });
      }
      const cashfree = (window as any).Cashfree({ mode: testMode ? 'sandbox' : 'production' });
      const result = await cashfree.subscriptionsCheckout({ subsSessionId: session, redirectTarget: '_self' });
      if (result?.error) throw new Error(result.error.message ?? 'Cashfree authorization did not complete.');
    } catch (error) {
      this.busy.set(false);
      this.error.set(error instanceof Error ? error.message : 'Could not open Cashfree checkout.');
      this.toast.error(this.error());
    }
  }
}

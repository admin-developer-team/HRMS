import { DecimalPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';

interface BillingPlan { code: string; name: string; currency: string; amountMinor: number; employeeLimit: number; providerPlanId: string }
interface BillingStatus { planCode: string; active: boolean; endsAt: string | null; pendingPlanCode: string | null; pendingStatus: string | null; pendingCheckoutUrl: string | null; testMode: boolean }
interface Checkout { subscriptionId: string; checkoutUrl: string }

@Component({
  selector: 'app-billing-page',
  imports: [DatePipe, DecimalPipe],
  template: `
    <main class="billing-page">
      <header><span class="eyebrow">Company administration</span><h1>Subscription & billing</h1><p>Choose a recurring plan for your company workspace.</p></header>
      @if (!auth.user()?.roles?.includes('TENANT_ADMIN')) {
        <p class="notice">Only the company administrator can manage billing.</p>
      } @else {
        @if (status(); as current) {
          <section class="current"><h2>Current subscription</h2><p><strong>{{ current.planCode }}</strong> · {{ current.active ? 'Enabled' : 'Inactive' }}</p>
            @if (current.endsAt) { <p>Paid access ends {{ current.endsAt | date:'mediumDate' }}</p> }
            @if (current.pendingPlanCode) { <p>Awaiting payment for {{ current.pendingPlanCode }}. Complete the Razorpay authorization to activate it.</p> }
            @if (current.pendingCheckoutUrl) { <a [href]="current.pendingCheckoutUrl">Continue authorization</a> }
            @if (current.testMode) { <p class="notice">Razorpay test mode: no real money is charged.</p> }
            <button type="button" (click)="loadStatus()">Refresh payment status</button>
          </section>
        }
        @if (error()) { <p class="error">{{ error() }}</p> }
        <section class="plans" aria-label="Available subscription plans">
          @for (plan of plans(); track plan.code) {
            <article><h2>{{ plan.name }}</h2><p class="price">₹{{ plan.amountMinor / 100 | number:'1.2-2' }} <small>/ billing cycle</small></p>
              <p>Up to {{ plan.employeeLimit }} employees</p>
              <button type="button" [disabled]="busy() || status()?.pendingStatus === 'pending' || status()?.pendingStatus === 'active' || status()?.pendingStatus === 'payment_pending'" (click)="checkout(plan)">Subscribe with Razorpay</button>
            </article>
          } @empty { <p>No billing plans are configured yet. Ask the platform administrator to add Razorpay plan IDs and pricing.</p> }
        </section>
      }
    </main>
  `,
  styles: [`
    .billing-page{max-width:1100px;margin:0 auto;padding:36px 24px 70px}h1{font-size:2rem;margin:8px 0}header p{color:#64748b}.eyebrow{color:#2563eb;text-transform:uppercase;font-size:.8rem;font-weight:700;letter-spacing:.1em}
    .current,.plans article{background:#fff;border:1px solid #e2e8f0;border-radius:18px;padding:24px;box-shadow:0 8px 24px #0f172a0a}.current{margin:24px 0}.current h2,.plans h2{margin-top:0}.plans{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:18px}.price{font-size:1.5rem;font-weight:700}.price small{font-size:.85rem;font-weight:400;color:#64748b}
    button{border:0;border-radius:9px;background:#2563eb;color:white;padding:12px 18px;cursor:pointer;font-weight:700}button:disabled{opacity:.5;cursor:not-allowed}.notice{color:#7c3aed}.error{color:#b91c1c}
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

  constructor() {
    if (!this.auth.user()?.roles.includes('TENANT_ADMIN')) return;
    this.api.get<BillingPlan[]>('/billing/plans').subscribe({ next: value => this.plans.set(value), error: () => this.error.set('Could not load billing plans.') });
    this.loadStatus();
  }

  loadStatus(): void {
    this.api.get<BillingStatus>('/billing/status').subscribe({
      next: value => {
        this.status.set(value);
        if (value.active && this.auth.session()?.billingOnly) {
          this.auth.refreshSession().subscribe({ next: () => void this.router.navigate(['/dashboard']), error: () => undefined });
        }
      },
      error: () => this.error.set('Could not load subscription status.'),
    });
  }

  checkout(plan: BillingPlan): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.api.post<Checkout>('/billing/checkout', { planCode: plan.code }).subscribe({
      next: result => { window.location.assign(result.checkoutUrl); },
      error: err => { this.busy.set(false); this.error.set(err.error?.detail ?? 'Could not create the Razorpay subscription.'); this.toast.error(this.error()); },
    });
  }
}

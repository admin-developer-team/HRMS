import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { BillingPage } from './billing.page';

describe('billing checkout', () => {
  it('starts a Razorpay checkout from the authorise button', () => {
    const post = vi.fn().mockReturnValue(of({
      subscriptionId: 'sub_test',
      checkoutUrl: 'https://rzp.io/rzp/test-checkout',
      provider: 'razorpay',
      testMode: true,
      publicKeyId: 'rzp_test_public',
    }));
    const api = {
      get: vi.fn((path: string) => of(path === '/billing/plans'
        ? [{ code: 'starter', name: 'Starter', currency: 'INR', amountMinor: 1000,
          employeeLimit: 50, providerPlanId: 'plan_test', provider: 'razorpay' }]
        : { planCode: 'trial', active: false, endsAt: null, pendingPlanCode: null,
          pendingStatus: null, pendingCheckoutUrl: null, testMode: true,
          trialEndsAt: null, pendingProvider: null })),
      post,
    };
    TestBed.configureTestingModule({
      imports: [BillingPage],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: AuthService, useValue: { user: () => ({ roles: ['TENANT_ADMIN'] }), session: () => null } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(BillingPage);
    const page = fixture.componentInstance;
    const openRazorpay = vi.spyOn(page, 'openRazorpay').mockResolvedValue();
    fixture.detectChanges();

    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(x => x.textContent?.includes('Authorize automatic billing'));
    expect(button).toBeDefined();
    expect(button!.disabled).toBe(false);
    button!.click();

    expect(post).toHaveBeenCalledWith('/billing/checkout',
      { planCode: 'starter', provider: 'razorpay', customerPhone: null });
    expect(openRazorpay).toHaveBeenCalledWith('sub_test', 'rzp_test_public');
  });

  it('opens Razorpay Standard Checkout on the company page', async () => {
    let options: any;
    const open = vi.fn();
    (window as any).Razorpay = class {
      constructor(value: any) { options = value; }
      open = open;
    };
    const api = { get: vi.fn().mockReturnValue(of([])) };
    TestBed.configureTestingModule({
      imports: [BillingPage],
      providers: [
        { provide: ApiService, useValue: api },
        { provide: AuthService, useValue: { user: () => ({ roles: ['TENANT_ADMIN'] }), session: () => null } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    });
    const page = TestBed.createComponent(BillingPage).componentInstance;
    await page.openRazorpay('sub_test', 'rzp_test_public');

    expect(open).toHaveBeenCalledOnce();
    expect(options.key).toBe('rzp_test_public');
    expect(options.subscription_id).toBe('sub_test');
    expect(options.handler).toEqual(expect.any(Function));
    delete (window as any).Razorpay;
  });
});

afterEach(() => TestBed.resetTestingModule());

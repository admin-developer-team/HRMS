import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authInterceptor, isPublicRequestUrl } from './auth.interceptor';

describe('auth interceptor public requests', () => {
  it('keeps account activation independent from saved login sessions', () => {
    expect(isPublicRequestUrl('/api/v1/public/activate')).toBe(true);
    expect(isPublicRequestUrl('/api/v1/public/trials')).toBe(true);
    expect(isPublicRequestUrl('/api/v1/public/trials/resend')).toBe(true);
  });

  it('still protects normal application APIs', () => {
    expect(isPublicRequestUrl('/api/v1/dashboard')).toBe(false);
  });

  it('does not attach or refresh a stale session for activation', () => {
    const auth = {
      accessToken: () => 'stale-access-token',
      tenantId: () => 'wrong-tenant-id',
      session: () => ({ billingOnly: false }),
      refreshSession: vi.fn(),
      clearMismatchedWorkspace: vi.fn(),
      logout: vi.fn(),
    };
    const router = { navigate: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);
    let failed = false;

    http.post('/api/v1/public/activate', { token: 'activation-token', password: 'password' })
      .subscribe({ error: () => { failed = true; } });
    const request = controller.expectOne('/api/v1/public/activate');
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.headers.has('X-Tenant-ID')).toBe(false);
    request.flush({ detail: 'invalid test token' }, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(auth.refreshSession).not.toHaveBeenCalled();
    expect(auth.logout).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
    controller.verify();
  });
});

afterEach(() => TestBed.resetTestingModule());

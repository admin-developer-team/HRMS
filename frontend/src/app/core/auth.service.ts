import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, shareReplay, tap } from 'rxjs';
import { ApiService } from './api.service';
import { UserSession } from './models';

const SESSION_KEY = 'peopleflow.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly state = signal<UserSession | null>(this.readSession());
  private rememberSession = Boolean(localStorage.getItem(SESSION_KEY));
  private refreshInFlight: Observable<UserSession> | null = null;

  readonly session = this.state.asReadonly();
  readonly user = computed(() => this.state()?.user ?? null);
  readonly tenantId = computed(() => this.state()?.user.tenantId ?? 'anonymous');
  readonly isAuthenticated = computed(() => Boolean(this.state()?.accessToken));
  readonly isAdmin = computed(
    () => this.user()?.permissions.includes('*') || this.user()?.permissions.includes('hr.manage'),
  );
  readonly isPlatformAdmin = computed(() => this.user()?.roles.includes('PLATFORM_ADMIN') ?? false);
  readonly isEmployee = computed(() => Boolean(this.user()?.employeeId));
  readonly isManager = computed(() => this.hasPermission('team.read'));

  login(
    payload: { tenantSlug: string; email: string; password: string },
    remember = true,
  ): Observable<UserSession> {
    this.rememberSession = remember;
    return this.api.login(payload).pipe(tap((session) => this.saveSession(session)));
  }

  accessToken(): string | null {
    return this.state()?.accessToken ?? null;
  }

  hasPermission(permission: string): boolean {
    const permissions = this.user()?.permissions ?? [];
    return permissions.includes('*') || permissions.includes(permission);
  }

  refreshSession(): Observable<UserSession> {
    const session = this.state();
    if (!session) throw new Error('No active session.');
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.api.refresh(session.refreshToken, session.user.tenantId).pipe(
        tap((updated) => this.saveSession(updated)),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return this.refreshInFlight;
  }

  logout(revoke = true): void {
    const refreshToken = this.state()?.refreshToken;
    if (revoke && refreshToken) {
      this.api
        .post<void>('/auth/revoke', { refreshToken })
        .pipe(finalize(() => this.clearSession()))
        .subscribe({ error: () => undefined });
      return;
    }
    this.clearSession();
  }

  private clearSession(): void {
    localStorage.removeItem(SESSION_KEY);
    sessionStorage.removeItem(SESSION_KEY);
    this.state.set(null);
    void this.router.navigate(['/login']);
  }

  private saveSession(session: UserSession): void {
    const storage = this.rememberSession ? localStorage : sessionStorage;
    const otherStorage = this.rememberSession ? sessionStorage : localStorage;
    storage.setItem(SESSION_KEY, JSON.stringify(session));
    otherStorage.removeItem(SESSION_KEY);
    this.state.set(session);
  }

  private readSession(): UserSession | null {
    try {
      const raw = localStorage.getItem(SESSION_KEY) ?? sessionStorage.getItem(SESSION_KEY);
      return raw ? (JSON.parse(raw) as UserSession) : null;
    } catch {
      return null;
    }
  }
}

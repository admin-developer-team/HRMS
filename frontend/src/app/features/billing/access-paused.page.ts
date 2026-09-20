import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-access-paused',
  template: `
    <main class="page">
      <section class="card">
        <div class="mark">P</div>
        <p class="eyebrow">PeopleFlow workspace</p>
        <h1>Workspace access is temporarily paused</h1>
        <p>Your company administrator is taking care of the subscription. Your employee records remain saved. Please contact your company administrator if you need access now.</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="button" (click)="check()" [disabled]="checking()">{{ checking() ? 'Checking…' : 'Check access again' }}</button>
          <button type="button" class="secondary" (click)="auth.logout(false)">Sign out</button>
        </div>
      </section>
    </main>
  `,
  styles: [`
    .page{min-height:100vh;display:grid;place-items:center;background:#f3f7fc;padding:24px;color:#17243a}.card{max-width:540px;background:white;border:1px solid #dce5f2;border-radius:22px;padding:42px;box-shadow:0 18px 55px #17243a12}.mark{width:48px;height:48px;border-radius:13px;background:#165ea6;color:white;display:grid;place-items:center;font-weight:800}.eyebrow{text-transform:uppercase;letter-spacing:.12em;color:#165ea6;font-size:.78rem;font-weight:700;margin-top:30px}h1{font-size:2rem;line-height:1.2;margin:8px 0 16px}p{color:#58657a;line-height:1.65}.actions{display:flex;gap:12px;flex-wrap:wrap;margin-top:28px}button{border:0;border-radius:10px;background:#165ea6;color:#fff;padding:12px 18px;font-weight:700;cursor:pointer}button:disabled{opacity:.6}.secondary{background:#edf3fb;color:#165ea6}.error{color:#ad2525}
  `],
})
export class AccessPausedPage {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly checking = signal(false);
  readonly error = signal('');

  check(): void {
    this.checking.set(true);
    this.error.set('');
    this.auth.refreshSession().subscribe({
      next: session => {
        this.checking.set(false);
        if (session.accessPaused) this.error.set('Access is still paused. Please check again later.');
        else void this.router.navigate([session.billingOnly ? '/billing' : this.auth.isEmployee() ? '/my' : '/dashboard']);
      },
      error: () => { this.checking.set(false); this.error.set('We could not check access right now. Please try again.'); },
    });
  }
}

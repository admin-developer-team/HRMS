import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-email-link-page',
  imports: [RouterLink],
  template: `
    <main style="min-height:100vh;display:grid;place-items:center;padding:24px;background:#f4f7fb">
      <section style="max-width:440px;width:100%;padding:32px;border-radius:16px;background:white;box-shadow:0 8px 32px #17203318;text-align:center">
        <h1 style="font-size:24px;color:#172033">Opening your workspace</h1>
        @if (error()) {
          <p role="alert">{{ error() }}</p>
          <a routerLink="/login">Sign in with your password</a>
        } @else {
          <p>Checking your secure email link…</p>
        }
      </section>
    </main>
  `,
})
export class EmailLinkPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  readonly error = signal('');

  constructor() {
    const token = this.route.snapshot.queryParamMap.get('token');
    history.replaceState(history.state, '', '/email-link');
    if (!token) {
      this.error.set('This email link is missing or invalid.');
      return;
    }
    this.auth.redeemEmailLink(token).subscribe({
      next: destination => {
        const safe = destination.startsWith('/') && !destination.startsWith('//') && !destination.includes('\\')
          ? destination : (this.auth.isEmployee() ? '/my' : '/dashboard');
        void this.router.navigateByUrl(safe, { replaceUrl: true });
      },
      error: (response: HttpErrorResponse) => this.error.set(
        response.error?.detail ?? 'This email link has expired or was already used. Sign in with your password.',
      ),
    });
  }
}

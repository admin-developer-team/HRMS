import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { workspaceSlug, workspaceUrl } from '../../core/workspace-url';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly passwordVisible = signal(false);
  readonly workspace = workspaceSlug();
  readonly workspaceName = signal('');
  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    remember: [true],
  });

  constructor() {
    if (this.route.snapshot.queryParamMap.get('workspaceMismatch') === '1') {
      this.error.set('This session belongs to a different workspace. Open your company subdomain and sign in there.');
    }
    const legacyTenant = this.route.snapshot.queryParamMap.get('tenant');
    if (this.workspace === 'platform' && legacyTenant && legacyTenant !== 'platform'
      && /^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$/.test(legacyTenant)) {
      const url = new URL(window.location.href);
      url.searchParams.delete('tenant');
      window.location.replace(`${workspaceUrl(legacyTenant)}${url.pathname}${url.search}`);
      return;
    }
    const email = this.route.snapshot.queryParamMap.get('email');
    this.form.patchValue({ ...(email ? { email } : {}) });
    this.api.get<{ slug: string; name: string }>('/auth/workspace').subscribe({
      next: result => this.workspaceName.set(result.name),
      error: () => this.error.set('This company workspace URL is unavailable.'),
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, password, remember } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set('');
    this.auth.login({ email, password }, remember).subscribe({
      next: () => {
        this.loading.set(false);
        const requested = this.route.snapshot.queryParamMap.get('returnUrl');
        const safeReturnUrl = requested?.startsWith('/') && !requested.startsWith('//') ? requested : null;
        if (this.auth.session()?.billingOnly) void this.router.navigate(['/billing']);
        else if (safeReturnUrl) void this.router.navigateByUrl(safeReturnUrl);
        else void this.router.navigate([this.auth.isEmployee() ? '/my' : this.auth.hasPermission('dashboard.admin') ? '/dashboard' : this.auth.hasPermission('support.read') ? '/support' : '/calendar']);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(
          error.error?.detail ??
            'We could not sign you in. Check your credentials and company URL.',
        );
      },
    });
  }
}

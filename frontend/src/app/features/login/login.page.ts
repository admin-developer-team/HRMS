import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly passwordVisible = signal(false);
  readonly form = this.fb.nonNullable.group({
    tenantSlug: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    remember: [true],
  });

  constructor() {
    const tenant = this.route.snapshot.queryParamMap.get('tenant');
    const email = this.route.snapshot.queryParamMap.get('email');
    this.form.patchValue({ ...(tenant ? { tenantSlug: tenant } : {}), ...(email ? { email } : {}) });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { tenantSlug, email, password, remember } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set('');
    this.auth.login({ tenantSlug, email, password }, remember).subscribe({
      next: () => {
        this.loading.set(false);
        const requested = this.route.snapshot.queryParamMap.get('returnUrl');
        const safeReturnUrl = requested?.startsWith('/') && !requested.startsWith('//') ? requested : null;
        if (safeReturnUrl) void this.router.navigateByUrl(safeReturnUrl);
        else void this.router.navigate([this.auth.isEmployee() ? '/my' : '/dashboard']);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(
          error.error?.detail ??
            'We could not sign you in. Check your company workspace and credentials.',
        );
      },
    });
  }
}

import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-password-recovery',
  imports: [ReactiveFormsModule, RouterLink],
  styleUrl: './login.page.scss',
  template: `
    <main class="login-form-side">
      <section class="login-card">
        <div class="form-heading"><h2>Reset your password</h2></div>
        @if (message()) { <p role="status">{{ message() }}</p> }
        @if (error()) { <p class="login-error" role="alert">{{ error() }}</p> }
        @if (step() === 'email') {
          <form [formGroup]="emailForm" (ngSubmit)="requestCode()">
            <div class="form-field"><label for="email">Registered email for this workspace</label>
              <div class="login-field"><input id="email" type="email" autocomplete="username" formControlName="email" required /></div></div>
            <button class="login-submit" [disabled]="busy() || emailForm.invalid">Send code</button>
          </form>
        } @else if (step() === 'code') {
          <form [formGroup]="resetForm" (ngSubmit)="reset()">
            <div class="form-field"><label for="code">8-digit email code</label>
              <div class="login-field"><input id="code" inputmode="numeric" autocomplete="one-time-code" maxlength="8" formControlName="code" /></div></div>
            <div class="form-field"><label for="newPassword">New password (at least 8 characters)</label>
              <div class="login-field"><input id="newPassword" type="password" autocomplete="new-password" formControlName="password" /></div></div>
            <div class="form-field"><label for="confirmPassword">Confirm new password</label>
              <div class="login-field"><input id="confirmPassword" type="password" autocomplete="new-password" formControlName="confirmation" /></div></div>
            <button class="login-submit" [disabled]="busy() || resetForm.invalid">Reset password</button>
            <button type="button" [disabled]="busy()" (click)="requestCode()">Resend code</button>
            <button type="button" [disabled]="busy()" (click)="changeEmail()">Change email</button>
          </form>
        }
        <p class="access-help"><a routerLink="/login" [queryParams]="{email: emailForm.controls.email.value}">Back to sign in</a></p>
      </section>
    </main>`,
})
export class PasswordRecoveryPage {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly step = signal<'email' | 'code' | 'done'>('email');
  readonly busy = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly emailForm = this.fb.group({
    email: [inject(ActivatedRoute).snapshot.queryParamMap.get('email') ?? '', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });
  readonly resetForm = this.fb.group({
    code: ['', [Validators.required, Validators.pattern(/^[0-9]{8}$/)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(256)]],
    confirmation: ['', Validators.required],
  });

  changeEmail(): void {
    this.step.set('email');
    this.resetForm.reset();
    this.message.set('');
    this.error.set('');
  }

  requestCode(): void {
    if (this.busy() || this.emailForm.invalid) return;
    this.busy.set(true);
    this.error.set('');
    this.api.post<void>('/auth/password-recovery/request', this.emailForm.getRawValue())
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: () => {
          this.step.set('code');
          this.resetForm.controls.code.reset();
          this.message.set('If this email belongs to an active account here, check your inbox for a code. It expires in 10 minutes. Wait one minute before requesting another.');
        },
        error: err => this.error.set(err.status === 429 ? 'Too many requests. Please try again later.' : 'Unable to request a code. Please try again.'),
      });
  }

  reset(): void {
    if (this.busy() || this.resetForm.invalid) return;
    const { code, password, confirmation } = this.resetForm.getRawValue();
    if (password !== confirmation) { this.error.set('The passwords do not match.'); return; }
    this.error.set('');
    this.busy.set(true);
    this.api.post<void>('/auth/password-recovery/confirm', { email: this.emailForm.controls.email.value, code, password })
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: () => {
          this.resetForm.reset();
          this.step.set('done');
          this.message.set('Your password has been reset. Sign in with your new password.');
        },
        error: err => this.error.set(err.status === 429 ? 'Too many attempts. Please try again later.' : err.error?.detail ?? 'Unable to reset your password. Please try again.'),
      });
  }
}

import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { CompanyProfileService } from '../../core/company-profile.service';
import { ToastService } from '../../core/toast.service';
import { SelfDashboard } from '../../core/models';
import { TenantTheme, ThemeService } from '../../core/theme.service';
import { DocumentComponent } from '../../shared/document/document.component';

@Component({
  selector: 'app-theme-studio-page',
  imports: [FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, MatSlideToggleModule, DocumentComponent],
  templateUrl: './theme-studio.page.html',
  styleUrl: './theme-studio.page.scss',
})
export class ThemeStudioPage {
  readonly themes = inject(ThemeService);
  readonly auth = inject(AuthService);
  readonly company = inject(CompanyProfileService);
  private readonly toast = inject(ToastService);
  private readonly api = inject(ApiService);
  readonly savingCompany = signal(false);
  readonly profile = signal<SelfDashboard['profile'] | null>(null);
  readonly phone = signal('');
  readonly savingProfile = signal(false);
  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly savingPassword = signal(false);
  readonly profileOwnerType = computed(() => this.auth.user()?.employeeId ? 'Employee' as const : 'User' as const);
  readonly profileOwnerId = computed(() => this.auth.user()?.employeeId ?? this.auth.user()?.id ?? '');
  readonly companyName = signal('');
  readonly legalName = signal('');
  readonly currency = signal('USD');
  readonly timeZone = signal('UTC');
  readonly locale = signal('en-US');
  readonly customPrimary = signal(this.themes.current().primary);
  readonly customAccent = signal(this.themes.current().accent);
  readonly customRadius = signal(this.themes.current().radius);
  readonly compact = signal(this.themes.current().density === 'compact');
  readonly dark = signal(this.themes.current().scheme === 'dark');

  constructor() {
    this.company.load().subscribe({ next: (profile) => {
      this.companyName.set(profile.name); this.legalName.set(profile.legalName ?? '');
      this.currency.set(profile.defaultCurrency); this.timeZone.set(profile.timeZone); this.locale.set(profile.locale);
    }, error: () => this.toast.error('Could not load company settings.') });
    if (this.auth.user()?.employeeId) this.loadProfile();
  }

  saveProfile(): void {
    this.savingProfile.set(true);
    this.api.put<SelfDashboard['profile']>('/me', { phone: this.phone().trim() || null })
      .pipe(finalize(() => this.savingProfile.set(false))).subscribe({
        next: (profile) => { this.profile.set(profile); this.phone.set(profile.phone ?? ''); this.toast.success('Profile updated.'); },
        error: () => this.toast.error('Could not update your profile.'),
      });
  }

  changePassword(): void {
    if (this.newPassword().length < 8) { this.toast.error('New password must be at least 8 characters.'); return; }
    if (this.newPassword() !== this.confirmPassword()) { this.toast.error('New password and confirmation do not match.'); return; }
    this.savingPassword.set(true);
    this.api.post<void>('/me/change-password', { currentPassword: this.currentPassword(), newPassword: this.newPassword() })
      .pipe(finalize(() => this.savingPassword.set(false))).subscribe({
        next: () => { this.toast.success('Password changed. Sign in again with your new password.'); this.auth.logout(false); },
        error: () => this.toast.error('Could not change your password. Check your current password.'),
      });
  }

  profilePhotoChanged(): void {
    this.toast.success('Profile photo updated.');
  }

  select(theme: TenantTheme): void {
    this.themes.select(theme);
    this.customPrimary.set(theme.primary);
    this.customAccent.set(theme.accent);
    this.customRadius.set(theme.radius);
    this.compact.set(theme.density === 'compact');
    this.dark.set(theme.scheme === 'dark');
    this.toast.success('Appearance updated.');
  }
  applyCustom(): void {
    this.themes.customize({
      primary: this.customPrimary(),
      primaryRgb: this.hexToRgb(this.customPrimary()),
      accent: this.customAccent(),
      radius: this.customRadius(),
      density: this.compact() ? 'compact' : 'comfortable',
      scheme: this.dark() ? 'dark' : 'light',
      surface: this.dark() ? '#08111f' : '#f5f7fb',
      sidebar: this.dark() ? '#030712' : '#071426',
    });
    this.toast.success('Appearance updated.');
  }

  saveCompany(): void {
    const profile = this.company.profile();
    if (!profile || !this.companyName().trim()) return;
    this.savingCompany.set(true);
    this.company.update({ name: this.companyName().trim(), legalName: this.legalName().trim() || undefined,
      defaultCurrency: this.currency().trim(), timeZone: this.timeZone().trim(), locale: this.locale().trim(), version: profile.version })
      .pipe(finalize(() => this.savingCompany.set(false))).subscribe({
        next: () => this.toast.success('Company identity updated.'),
        error: () => this.toast.error('Could not update company identity.'),
      });
  }

  refreshCompanyBranding(): void {
    this.company.load().subscribe({ error: () => this.toast.error('Could not refresh company branding.') });
  }

  private loadProfile(): void {
    this.api.get<SelfDashboard['profile']>('/me').subscribe({
      next: (profile) => { this.profile.set(profile); this.phone.set(profile.phone ?? ''); },
      error: () => this.toast.error('Could not load your profile settings.'),
    });
  }

  private hexToRgb(hex: string): string {
    const clean = hex.replace('#', '');
    const value = parseInt(
      clean.length === 3
        ? clean
            .split('')
            .map((x) => x + x)
            .join('')
        : clean,
      16,
    );
    return `${(value >> 16) & 255} ${(value >> 8) & 255} ${value & 255}`;
  }
}

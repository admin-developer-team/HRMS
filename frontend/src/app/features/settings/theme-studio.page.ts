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
import { Employee, SelfDashboard } from '../../core/models';
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
  readonly managedEmployee = signal<Employee | null>(null);
  readonly firstName = signal('');
  readonly lastName = signal('');
  readonly workEmail = signal('');
  readonly employeeNumber = signal('');
  readonly hireDate = signal('');
  readonly phone = signal('');
  readonly savingProfile = signal(false);
  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly savingPassword = signal(false);
  readonly emailConfiguration = signal<EmailConfiguration | null>(null);
  readonly emailTemplates = signal<EmailTemplate[]>([]);
  readonly selectedEmailTemplateId = signal('');
  readonly smtpEnabled = signal(false);
  readonly smtpHost = signal('');
  readonly smtpPort = signal(587);
  readonly smtpUsername = signal('');
  readonly smtpPassword = signal('');
  readonly smtpUseTls = signal(true);
  readonly smtpFromEmail = signal('');
  readonly smtpFromName = signal('');
  readonly smtpReplyTo = signal('');
  readonly applicationBaseUrl = signal('');
  readonly testRecipient = signal('');
  readonly savingEmail = signal(false);
  readonly testingEmail = signal(false);
  readonly savingTemplate = signal(false);
  readonly selectedEmailTemplate = computed(() => this.emailTemplates().find(x => x.id === this.selectedEmailTemplateId()) ?? null);
  readonly templateSubject = signal('');
  readonly templateHtml = signal('');
  readonly templateText = signal('');
  readonly templateEnabled = signal(true);
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
    if (this.auth.user()?.employeeId) {
      this.loadProfile();
      if (this.auth.hasPermission('employees.manage')) this.loadManagedEmployee();
    }
    if (this.auth.isPlatformAdmin()) this.loadEmailSettings();
  }

  saveProfile(): void {
    this.savingProfile.set(true);
    const employee = this.managedEmployee();
    if (employee) {
      this.api.put<Employee>(`/employees/${employee.id}`, {
        employeeNumber: this.employeeNumber().trim(), firstName: this.firstName().trim(), lastName: this.lastName().trim(),
        workEmail: this.workEmail().trim(), phone: this.phone().trim() || null, hireDate: this.hireDate(),
        status: employee.status, employmentType: employee.employmentType, departmentId: employee.departmentId ?? null,
        designationId: employee.designationId ?? null, locationId: employee.locationId ?? null, managerId: employee.managerId ?? null,
        baseSalary: employee.baseSalary, salaryCurrency: employee.salaryCurrency, version: employee.version,
      }).pipe(finalize(() => this.savingProfile.set(false))).subscribe({
        next: (updated) => {
          this.setManagedEmployee(updated); this.loadProfile();
          this.auth.refreshSession().subscribe({ next: () => this.toast.success('Profile and login identity updated.'), error: () => this.toast.success('Profile updated. Sign in again to refresh your account header.') });
        },
        error: () => this.toast.error('Could not update your profile.'),
      });
      return;
    }
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
    this.api.post<void>('/account/change-password', { currentPassword: this.currentPassword(), newPassword: this.newPassword() })
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

  saveEmailConfiguration(): void {
    const current = this.emailConfiguration();
    if (!current) return;
    this.savingEmail.set(true);
    this.api.put<EmailConfiguration>('/email-settings', {
      isEnabled: this.smtpEnabled(), host: this.smtpHost().trim(), port: this.smtpPort(),
      username: this.smtpUsername().trim() || null, password: this.smtpPassword() || null,
      useTls: this.smtpUseTls(), fromEmail: this.smtpFromEmail().trim(), fromName: this.smtpFromName().trim(),
      replyToEmail: this.smtpReplyTo().trim() || null, applicationBaseUrl: this.applicationBaseUrl().trim() || null,
      version: current.version,
    }).pipe(finalize(() => this.savingEmail.set(false))).subscribe({
      next: value => { this.setEmailConfiguration(value); this.smtpPassword.set(''); this.loadTemplates(); this.toast.success('Email configuration saved.'); },
      error: () => this.toast.error('Could not save email configuration.'),
    });
  }

  sendTestEmail(): void {
    this.testingEmail.set(true);
    this.api.post<void>('/email-settings/test', { recipientEmail: this.testRecipient().trim() || null })
      .pipe(finalize(() => this.testingEmail.set(false))).subscribe({
        next: () => this.toast.success('Test email sent.'),
        error: () => this.toast.error('Test email failed. Check the SMTP host, port, credentials, sender verification, and TLS setting.'),
      });
  }

  selectEmailTemplate(id: string): void {
    this.selectedEmailTemplateId.set(id);
    const template = this.emailTemplates().find(x => x.id === id);
    if (!template) return;
    this.templateSubject.set(template.subjectTemplate); this.templateHtml.set(template.htmlTemplate);
    this.templateText.set(template.textTemplate ?? ''); this.templateEnabled.set(template.isEnabled);
  }

  saveEmailTemplate(): void {
    const template = this.selectedEmailTemplate();
    if (!template) return;
    this.savingTemplate.set(true);
    this.api.put<EmailTemplate>(`/email-settings/templates/${template.id}`, {
      subjectTemplate: this.templateSubject(), htmlTemplate: this.templateHtml(), textTemplate: this.templateText() || null,
      isEnabled: this.templateEnabled(), version: template.version,
    }).pipe(finalize(() => this.savingTemplate.set(false))).subscribe({
      next: updated => {
        this.emailTemplates.update(rows => rows.map(x => x.id === updated.id ? updated : x));
        this.selectEmailTemplate(updated.id); this.toast.success('Email template saved.');
      },
      error: () => this.toast.error('Could not save email template.'),
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

  private loadManagedEmployee(): void {
    const id = this.auth.user()?.employeeId;
    if (!id) return;
    this.api.get<Employee>(`/employees/${id}`).subscribe({
      next: (employee) => this.setManagedEmployee(employee),
      error: () => this.toast.error('Could not load the administrative profile editor.'),
    });
  }

  private loadEmailSettings(): void {
    this.api.get<EmailConfiguration>('/email-settings').subscribe({
      next: value => { this.setEmailConfiguration(value); this.testRecipient.set(this.auth.user()?.email ?? ''); },
      error: () => this.toast.error('Could not load email configuration.'),
    });
    this.loadTemplates();
  }

  private loadTemplates(): void {
    this.api.get<EmailTemplate[]>('/email-settings/templates').subscribe({
      next: rows => { this.emailTemplates.set(rows); if (rows.length) this.selectEmailTemplate(rows[0].id); },
      error: () => this.toast.error('Could not load email templates.'),
    });
  }

  private setEmailConfiguration(value: EmailConfiguration): void {
    this.emailConfiguration.set(value); this.smtpEnabled.set(value.isEnabled); this.smtpHost.set(value.host);
    this.smtpPort.set(value.port); this.smtpUsername.set(value.username ?? ''); this.smtpUseTls.set(value.useTls);
    this.smtpFromEmail.set(value.fromEmail); this.smtpFromName.set(value.fromName); this.smtpReplyTo.set(value.replyToEmail ?? '');
    // The settings screen is served by the public frontend, so its origin is the
    // authoritative deployment URL (localhost in development, the server in production).
    this.applicationBaseUrl.set(globalThis.location?.origin ?? value.applicationBaseUrl ?? '');
  }

  private setManagedEmployee(employee: Employee): void {
    this.managedEmployee.set(employee); this.firstName.set(employee.firstName); this.lastName.set(employee.lastName);
    this.workEmail.set(employee.workEmail); this.employeeNumber.set(employee.employeeNumber); this.hireDate.set(employee.hireDate);
    this.phone.set(employee.phone ?? '');
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

interface EmailConfiguration {
  isEnabled: boolean; host: string; port: number; username?: string; hasPassword: boolean; useTls: boolean;
  fromEmail: string; fromName: string; replyToEmail?: string; applicationBaseUrl?: string; version: number;
}

interface EmailTemplate {
  id: string; key: string; name: string; subjectTemplate: string; htmlTemplate: string; textTemplate?: string;
  isEnabled: boolean; isSystem: boolean; version: number;
}

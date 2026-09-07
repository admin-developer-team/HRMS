import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { CompanyProfileService } from '../core/company-profile.service';
import { DocumentService } from '../core/document.service';
import { LoadingService } from '../core/loading.service';
import { UserNotification } from '../core/models';
import { NotificationService } from '../core/notification.service';
import { ToastService } from '../core/toast.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: string;
  permission?: string;
  platformOnly?: boolean;
  employeeOnly?: boolean;
}
interface NavSection {
  label: string;
  items: NavItem[];
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    DatePipe,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnDestroy {
  readonly auth = inject(AuthService);
  readonly loading = inject(LoadingService);
  readonly company = inject(CompanyProfileService);
  readonly notifications = inject(NotificationService);
  readonly toasts = inject(ToastService);
  private readonly documents = inject(DocumentService);
  private readonly breakpoint = inject(BreakpointObserver);
  private readonly router = inject(Router);
  private readonly profilePhotoSubscription = this.documents.profilePhotoChanged$.subscribe(
    (employeeId) => {
      if (employeeId === (this.auth.user()?.employeeId ?? this.auth.user()?.id)) this.refreshProfilePhoto();
    },
  );

  readonly mobile = signal(false);
  readonly collapsed = signal(false);
  readonly companyLogoUrl = signal<string | null>(null);
  readonly profilePhotoUrl = signal<string | null>(null);
  private readonly companyLogoEffect = effect(() => this.loadImage(this.company.profile()?.logoDocumentId, this.companyLogoUrl));
  readonly initials = computed(() =>
    (this.auth.user()?.displayName ?? 'HR')
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );
  readonly companyName = computed(() => this.company.profile()?.name ?? 'PeopleFlow');

  readonly navigation: NavSection[] = [
    {
      label: 'Overview',
      items: [
        {
          label: 'Admin dashboard',
          icon: 'space_dashboard',
          route: '/dashboard',
          permission: 'dashboard.admin',
        },
        { label: 'My workspace', icon: 'home', route: '/my', employeeOnly: true },
        { label: 'My services', icon: 'apps', route: '/my-services', employeeOnly: true },
        { label: 'My team', icon: 'groups', route: '/my-team', permission: 'team.read' },
      ],
    },
    {
      label: 'Platform',
      items: [
        { label: 'Customer companies', icon: 'domain', route: '/companies', platformOnly: true },
      ],
    },
    {
      label: 'People',
      items: [
        { label: 'Employees', icon: 'group', route: '/employees', permission: 'employees.read' },
        {
          label: 'Organization',
          icon: 'account_tree',
          route: '/organization',
          permission: 'employees.read',
        },
        { label: 'Leave', icon: 'beach_access', route: '/leave', permission: 'leave.manage' },
        {
          label: 'Attendance',
          icon: 'schedule',
          route: '/attendance',
          permission: 'attendance.manage',
        },
        {
          label: 'Workforce',
          icon: 'calendar_month',
          route: '/workforce',
          permission: 'workforce.manage',
        },
      ],
    },
    {
      label: 'Compensation',
      items: [
        { label: 'Payroll', icon: 'payments', route: '/payroll', permission: 'payroll.manage' },
        {
          label: 'Expenses',
          icon: 'receipt_long',
          route: '/expenses',
          permission: 'expenses.manage',
        },
      ],
    },
    {
      label: 'Delivery',
      items: [
        {
          label: 'Work management',
          icon: 'view_kanban',
          route: '/work',
          permission: 'work.read',
        },
      ],
    },
    {
      label: 'Talent',
      items: [
        {
          label: 'Recruitment',
          icon: 'person_search',
          route: '/recruitment',
          permission: 'recruitment.manage',
        },
        {
          label: 'Performance',
          icon: 'monitoring',
          route: '/performance',
          permission: 'performance.manage',
        },
        { label: 'Learning', icon: 'school', route: '/training', permission: 'training.manage' },
      ],
    },
    {
      label: 'Operations',
      items: [
        { label: 'Assets', icon: 'laptop_mac', route: '/assets', permission: 'assets.manage' },
        {
          label: 'Access & roles',
          icon: 'admin_panel_settings',
          route: '/identity',
          permission: 'identity.manage',
        },
        { label: 'Audit log', icon: 'history', route: '/audit', permission: 'audit.read' },
      ],
    },
  ];

  constructor() {
    this.breakpoint
      .observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .subscribe((state) => this.mobile.set(state.matches));
    this.company.load().subscribe({ error: () => undefined });
    this.refreshProfilePhoto();
    this.refreshNotifications();
    this.notifications.connect();
  }

  ngOnDestroy(): void {
    this.notifications.disconnect();
    this.profilePhotoSubscription.unsubscribe();
    this.revoke(this.companyLogoUrl());
    this.revoke(this.profilePhotoUrl());
  }

  toggleNavigation(): void {
    if (!this.mobile()) this.collapsed.update((value) => !value);
  }

  visible(item: NavItem): boolean {
    if (item.platformOnly) return this.auth.isPlatformAdmin();
    if (item.employeeOnly && !this.auth.isEmployee()) return false;
    if (item.permission && !this.auth.hasPermission(item.permission)) return false;
    return true;
  }

  sectionVisible(section: NavSection): boolean {
    return section.items.some((item) => this.visible(item));
  }

  navigateSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (value && this.auth.hasPermission('employees.read'))
      void this.router.navigate(['/employees'], { queryParams: { search: value } });
    else if (value) void this.router.navigate(['/my-services']);
  }

  openNotification(item: UserNotification): void {
    if (!item.isRead) this.notifications.markRead(item.id).subscribe({ error: () => undefined });
    if (item.link) void this.router.navigateByUrl(item.link);
  }

  markAllNotificationsRead(): void {
    this.notifications.markAllRead().subscribe({ error: () => undefined });
  }

  notificationIcon(kind: string): string {
    return ({
      work: 'task_alt',
      leave: 'event_available',
      timesheet: 'schedule',
      expense: 'receipt_long',
      payroll: 'payments',
      asset: 'laptop_mac',
      training: 'school',
      performance: 'monitoring',
      recruitment: 'person_search',
      document: 'verified',
      announcement: 'campaign',
      security: 'security',
    } as Record<string, string>)[kind] ?? 'notifications';
  }

  private refreshNotifications(): void {
    this.notifications.load().subscribe({ error: () => undefined });
  }

  private refreshProfilePhoto(): void {
    const user = this.auth.user();
    const ownerId = user?.employeeId ?? user?.id;
    if (!ownerId) return;
    this.documents.list(user?.employeeId ? 'Employee' : 'User', ownerId, 'profile').subscribe({
      next: (items) => this.loadImage(items[0]?.id, this.profilePhotoUrl),
      error: () => undefined,
    });
  }

  private loadImage(id: string | undefined, target: { set(value: string | null): void; (): string | null }): void {
    if (!id) { this.revoke(target()); target.set(null); return; }
    this.documents.content(id).subscribe({ next: (blob) => {
      this.revoke(target());
      target.set(URL.createObjectURL(blob));
    }, error: () => undefined });
  }

  private revoke(url: string | null): void { if (url) URL.revokeObjectURL(url); }
}

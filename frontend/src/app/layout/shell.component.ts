import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { CompanyProfileService } from '../core/company-profile.service';
import { DocumentService } from '../core/document.service';
import { LoadingService } from '../core/loading.service';
import { Employee, PagedResult, UserNotification, WorkItem, WorkProject } from '../core/models';
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

interface GlobalSearchResult {
  kind: 'Action' | 'Page' | 'Employee' | 'Ticket' | 'Project';
  title: string;
  subtitle: string;
  icon: string;
  route: string;
  queryParams?: Record<string, string>;
  keywords?: string;
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
  private readonly api = inject(ApiService);
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
  readonly globalSearchQuery = signal('');
  readonly globalSearchOpen = signal(false);
  readonly globalSearchBusy = signal(false);
  readonly globalSearchResults = signal<GlobalSearchResult[]>([]);
  readonly globalSearchActive = signal(0);
  private globalSearchTimer?: ReturnType<typeof setTimeout>;
  private globalSearchGeneration = 0;
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
    if (this.globalSearchTimer) clearTimeout(this.globalSearchTimer);
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

  onGlobalSearch(value: string): void {
    this.globalSearchQuery.set(value);
    this.globalSearchOpen.set(true);
    this.globalSearchActive.set(0);
    if (this.globalSearchTimer) clearTimeout(this.globalSearchTimer);
    this.globalSearchTimer = setTimeout(() => this.runGlobalSearch(value), 180);
  }

  showGlobalSearch(): void {
    this.globalSearchOpen.set(true);
    if (!this.globalSearchResults().length) this.runGlobalSearch(this.globalSearchQuery());
  }

  closeGlobalSearchSoon(): void {
    setTimeout(() => this.globalSearchOpen.set(false), 150);
  }

  globalSearchKeydown(event: KeyboardEvent): void {
    const results = this.globalSearchResults();
    if (event.key === 'ArrowDown' && results.length) {
      event.preventDefault();
      this.globalSearchActive.update(index => (index + 1) % results.length);
    } else if (event.key === 'ArrowUp' && results.length) {
      event.preventDefault();
      this.globalSearchActive.update(index => (index - 1 + results.length) % results.length);
    } else if (event.key === 'Enter' && results.length) {
      event.preventDefault();
      this.openGlobalSearchResult(results[this.globalSearchActive()] ?? results[0]);
    } else if (event.key === 'Escape') {
      this.globalSearchOpen.set(false);
    }
  }

  openGlobalSearchResult(result: GlobalSearchResult): void {
    this.globalSearchOpen.set(false);
    this.globalSearchQuery.set('');
    this.globalSearchResults.set([]);
    void this.router.navigate([result.route], { queryParams: result.queryParams });
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

  private runGlobalSearch(rawQuery: string): void {
    const query = rawQuery.trim();
    const generation = ++this.globalSearchGeneration;
    const commands = this.searchCommands()
      .map(result => ({ result, score: this.searchScore(`${result.title} ${result.subtitle} ${result.keywords ?? ''}`, query) }))
      .filter(match => !query || match.score > 0)
      .sort((a, b) => b.score - a.score)
      .slice(0, query ? 7 : 6)
      .map(match => match.result);
    if (query.length < 2) {
      this.globalSearchBusy.set(false);
      this.globalSearchResults.set(commands);
      return;
    }
    this.globalSearchBusy.set(true);
    const emptyPage = <T>(): PagedResult<T> => ({ items: [], page: 1, pageSize: 0, total: 0, totalPages: 0 });
    const employees = this.auth.hasPermission('employees.read')
      ? this.api.get<PagedResult<Employee>>('/employees', { page: 1, pageSize: 6, search: query }).pipe(catchError(() => of(emptyPage<Employee>())))
      : of(emptyPage<Employee>());
    const tickets = this.auth.hasPermission('work.read')
      ? this.api.get<PagedResult<WorkItem>>('/work/items', { page: 1, pageSize: 6, search: query }).pipe(catchError(() => of(emptyPage<WorkItem>())))
      : of(emptyPage<WorkItem>());
    const projects = this.auth.hasPermission('work.read')
      ? this.api.get<WorkProject[]>('/work/projects').pipe(catchError(() => of([] as WorkProject[])))
      : of([] as WorkProject[]);
    forkJoin({ employees, tickets, projects }).subscribe(({ employees, tickets, projects }) => {
      if (generation !== this.globalSearchGeneration) return;
      const dynamic: GlobalSearchResult[] = [
        ...employees.items.map(employee => ({ kind: 'Employee' as const, title: employee.fullName, subtitle: `${employee.employeeNumber} · ${employee.workEmail}`, icon: 'person', route: `/employees/${employee.id}` })),
        ...tickets.items.map(ticket => ({ kind: 'Ticket' as const, title: `${ticket.key} · ${ticket.summary}`, subtitle: `${ticket.projectKey} · ${ticket.status}`, icon: 'confirmation_number', route: '/work', queryParams: { item: ticket.id } })),
        ...projects.filter(project => this.searchScore(`${project.key} ${project.name}`, query) > 0).slice(0, 5)
          .map(project => ({ kind: 'Project' as const, title: `${project.key} · ${project.name}`, subtitle: `${project.memberCount} project members`, icon: 'folder', route: '/work', queryParams: { project: project.id } })),
      ];
      this.globalSearchResults.set([...commands, ...dynamic].slice(0, 15));
      this.globalSearchActive.set(0);
      this.globalSearchBusy.set(false);
    });
  }

  private searchCommands(): GlobalSearchResult[] {
    const results: GlobalSearchResult[] = [];
    for (const section of this.navigation) for (const item of section.items) if (this.visible(item)) results.push({
      kind: 'Page', title: item.label, subtitle: section.label, icon: item.icon, route: item.route, keywords: `${section.label} open go navigate`,
    });
    results.push({ kind: 'Page', title: 'Settings', subtitle: 'Company and personal settings', icon: 'settings', route: '/settings', keywords: 'theme configuration profile' });
    if (this.auth.isEmployee()) results.push(
      { kind: 'Action', title: 'Apply for leave', subtitle: 'Create a new time-off request', icon: 'beach_access', route: '/my-services', queryParams: { view: 'Leave', action: 'create' }, keywords: 'request holiday vacation sick annual time off absence' },
      { kind: 'Action', title: 'View my leave balance', subtitle: 'Check available and used leave', icon: 'account_balance_wallet', route: '/my-services', queryParams: { view: 'Leave balances' }, keywords: 'remaining holiday allowance time off' },
      { kind: 'Action', title: 'Submit a timesheet', subtitle: 'Record working hours', icon: 'schedule', route: '/my-services', queryParams: { view: 'Timesheets', action: 'create' }, keywords: 'worklog work log time hours' },
      { kind: 'Action', title: 'Submit an expense claim', subtitle: 'Create a claim and attach receipts', icon: 'receipt_long', route: '/my-services', queryParams: { view: 'Expenses', action: 'create' }, keywords: 'reimbursement bill receipt money travel expense' },
      { kind: 'Page', title: 'My attendance', subtitle: 'Review attendance, hours and lateness', icon: 'schedule', route: '/my-services', queryParams: { view: 'My attendance' }, keywords: 'clock check in check out present absent overtime' },
      { kind: 'Page', title: 'My payslips', subtitle: 'Open salary slips and payroll documents', icon: 'payments', route: '/my-services', queryParams: { view: 'Payslips' }, keywords: 'salary pay slip compensation wage download' },
      { kind: 'Page', title: 'My documents', subtitle: 'Find your HR documents', icon: 'folder_shared', route: '/my-services', queryParams: { view: 'Documents' }, keywords: 'files letters certificates upload download' },
      { kind: 'Page', title: 'My learning', subtitle: 'View assigned courses and training', icon: 'school', route: '/my-services', queryParams: { view: 'Learning' }, keywords: 'course training certification learn' },
      { kind: 'Page', title: 'My performance reviews', subtitle: 'Open goals and self assessments', icon: 'monitoring', route: '/my-services', queryParams: { view: 'Performance' }, keywords: 'appraisal review goals rating assessment' },
      { kind: 'Page', title: 'My assigned assets', subtitle: 'See equipment issued to you', icon: 'laptop_mac', route: '/my-services', queryParams: { view: 'My assets' }, keywords: 'laptop equipment device inventory' },
    );
    if (this.auth.hasPermission('work.read')) results.push({ kind: 'Action', title: 'Find a ticket to log time', subtitle: 'Open a ticket and add a worklog', icon: 'timer', route: '/work', keywords: 'worklog work log hours ticket task' });
    if (this.auth.hasPermission('work.create')) results.push({ kind: 'Action', title: 'Create a ticket', subtitle: 'Add work to the selected project', icon: 'add_task', route: '/work', queryParams: { action: 'create' }, keywords: 'new task issue bug story work item' });
    if (this.auth.hasPermission('leave.manage')) results.push({ kind: 'Action', title: 'Create leave for an employee', subtitle: 'Submit leave on behalf of someone', icon: 'event_available', route: '/leave', queryParams: { view: 'Requests', action: 'create' }, keywords: 'admin apply absence holiday' });
    return results;
  }

  private searchScore(value: string, rawQuery: string): number {
    if (!rawQuery) return 1;
    const text = value.toLocaleLowerCase();
    const tokens = rawQuery.toLocaleLowerCase().split(/\s+/).filter(Boolean);
    let score = 0;
    for (const token of tokens) {
      const index = text.indexOf(token);
      if (index >= 0) { score += 100 - Math.min(index, 60); continue; }
      let cursor = 0;
      for (const character of text) if (character === token[cursor]) cursor++;
      if (cursor !== token.length) return 0;
      score += 20;
    }
    return score;
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

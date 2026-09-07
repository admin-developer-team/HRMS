import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Dashboard } from '../../core/models';

@Component({
  selector: 'app-dashboard-page',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, MatButtonModule, MatIconModule, RouterLink],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly dashboard = signal<Dashboard>({
    activeEmployees: 0,
    pendingLeaveRequests: 0,
    openJobs: 0,
    availableAssets: 0,
    currentPayrollTotal: 0,
    employeesByStatus: {},
  });
  readonly today = new Date();
  readonly actions = [
    { label: 'Add employee', icon: 'person_add', route: '/employees' },
    { label: 'Run payroll', icon: 'payments', route: '/payroll' },
    { label: 'Post a job', icon: 'work', route: '/recruitment' },
    { label: 'Review leave', icon: 'event_available', route: '/leave' },
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.dashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Unable to load workforce data. Check the connection and try again.');
      },
    });
  }

  statusEntries(): { label: string; value: number; percent: number }[] {
    const entries = Object.entries(this.dashboard().employeesByStatus ?? {});
    const total = Math.max(
      1,
      entries.reduce((sum, [, value]) => sum + value, 0),
    );
    return entries.map(([label, value]) => ({
      label,
      value,
      percent: Math.round((value / total) * 100),
    }));
  }

  exportReport(): void {
    const data = this.dashboard();
    const rows = [
      ['Metric', 'Value'],
      ['Active employees', data.activeEmployees],
      ['Pending leave requests', data.pendingLeaveRequests],
      ['Open jobs', data.openJobs],
      ['Available assets', data.availableAssets],
      ['Current payroll total', data.currentPayrollTotal],
      ...Object.entries(data.employeesByStatus).map(([status, value]) => [
        `Employees: ${status}`,
        value,
      ]),
    ];
    const csv = rows
      .map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(','))
      .join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'hrms-dashboard.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }
}

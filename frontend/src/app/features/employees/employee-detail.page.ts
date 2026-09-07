import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Employee, PagedResult } from '../../core/models';
import { DocumentComponent } from '../../shared/document/document.component';

interface DailyAttendanceReport {
  employeeId: string;
  employeeName: string;
  workDate: string;
  firstCheckIn?: string;
  lastCheckOut?: string;
  sessionCount: number;
  totalHours: number;
  requiredHours: number;
  lateMinutes: number;
  earlyDepartureMinutes: number;
  overtimeHours: number;
  shortfallHours: number;
  status: string;
}

interface AttendanceSummary {
  employeeId: string;
  employeeName: string;
  from: string;
  to: string;
  scheduledDays: number;
  daysPresent: number;
  daysAbsent: number;
  daysOnLeave: number;
  totalHours: number;
  averageHours: number;
  lateDays: number;
  earlyDepartureDays: number;
  shortDays: number;
  overtimeHours: number;
}

interface LoginHistory {
  loggedInAt: string;
  ipAddress?: string;
  userAgent?: string;
}

@Component({
  selector: 'app-employee-detail-page',
  standalone: true,
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule, DocumentComponent],
  templateUrl: './employee-detail.page.html',
  styleUrl: './employee-detail.page.scss',
})
export class EmployeeDetailPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  readonly auth = inject(AuthService);
  readonly employee = signal<Employee | null>(null);
  readonly report = signal<DailyAttendanceReport[]>([]);
  readonly summary = signal<AttendanceSummary | null>(null);
  readonly logins = signal<LoginHistory[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly from = signal('');
  readonly to = signal('');

  ngOnInit(): void {
    const today = new Date();
    this.to.set(this.iso(today));
    this.from.set(this.iso(new Date(today.getFullYear(), today.getMonth(), 1)));
    this.load();
  }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      employee: this.api.get<Employee>(`/employees/${id}`),
      report: this.api.get<PagedResult<DailyAttendanceReport>>('/attendance/report', { employeeId: id, from: this.from(), to: this.to(), pageSize: 200 }).pipe(catchError(() => of({ items: [], page: 1, pageSize: 200, total: 0, totalPages: 0 }))),
      summary: this.api.get<AttendanceSummary[]>('/attendance/summary', { employeeId: id, from: this.from(), to: this.to() }).pipe(catchError(() => of([]))),
      logins: this.api.get<LoginHistory[]>(`/employees/${id}/login-history`, { take: 50 }).pipe(catchError(() => of([]))),
    }).subscribe({
      next: ({ employee, report, summary, logins }) => {
        this.employee.set(employee);
        this.report.set(report.items);
        this.summary.set(summary[0] ?? null);
        this.logins.set(logins);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.error?.detail ?? 'Unable to load this employee profile.');
        this.loading.set(false);
      },
    });
  }

  private iso(value: Date): string {
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
  }

  duration(value: number): string {
    const minutes = Math.max(0, Math.round((value || 0) * 60));
    const hours = Math.floor(minutes / 60);
    const remainder = minutes % 60;
    return hours ? `${hours}h${remainder ? ` ${remainder}m` : ''}` : `${remainder}m`;
  }

  minutes(value: number): string {
    const total = Math.max(0, Math.round(value || 0));
    if (!total) return '—';
    const hours = Math.floor(total / 60);
    const remainder = total % 60;
    return hours ? `${hours}h${remainder ? ` ${remainder}m` : ''}` : `${remainder}m`;
  }
}

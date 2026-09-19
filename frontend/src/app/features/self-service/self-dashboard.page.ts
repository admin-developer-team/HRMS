import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { DocumentService } from '../../core/document.service';
import { AttendanceRecord, PagedResult, SelfDashboard } from '../../core/models';

@Component({
  selector: 'app-self-dashboard',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './self-dashboard.page.html',
  styleUrl: './self-dashboard.page.scss',
})
export class SelfDashboardPage implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly documents = inject(DocumentService);
  readonly auth = inject(AuthService);
  readonly loading = signal(true);
  readonly clocking = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly locationStatus = signal('Location is requested only when you check in or out.');
  readonly lastLocationUrl = signal<string | null>(null);
  readonly dashboard = signal<SelfDashboard | null>(null);
  readonly history = signal<AttendanceRecord[]>([]);
  readonly now = signal(new Date());
  readonly needsCorrection = computed(() => {
    const session = this.dashboard()?.todayAttendance;
    return !!session?.clockedInAt && !session.clockedOutAt && this.now().getTime() - new Date(session.clockedInAt).getTime() > 24 * 60 * 60 * 1000;
  });
  readonly profilePhotoUrl = signal<string | null>(null);
  private clockTimer?: number;
  private locationAttempt = 0;

  ngOnInit(): void {
    this.load();
    this.loadProfilePhoto();
    this.clockTimer = window.setInterval(() => this.now.set(new Date()), 30_000);
  }

  ngOnDestroy(): void {
    if (this.clockTimer) window.clearInterval(this.clockTimer);
    this.revokeProfilePhoto();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<SelfDashboard>('/me/dashboard').subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(error.error?.detail ?? 'Unable to load your workspace.');
      },
    });
    this.api
      .get<PagedResult<AttendanceRecord>>('/me/attendance', { page: 1, pageSize: 10 })
      .subscribe({ next: (result) => this.history.set(result.items ?? []) });
  }

  clock(action: 'clock-in' | 'clock-out'): void {
    if (this.clocking()) return;
    this.error.set('');
    this.success.set('');
    this.lastLocationUrl.set(null);
    this.clocking.set(true);
    const attempt = ++this.locationAttempt;
    const location = this.dashboard()?.requireLocationCapture ? this.getLocation() : null;
    if (location) this.locationStatus.set('Recording your time. Location is being checked in the background…');
    this.api
      .post<AttendanceRecord>(`/me/attendance/${action}`, {
        source: 'web',
      })
      .pipe(finalize(() => this.clocking.set(false)))
      .subscribe({
        next: record => {
          this.success.set(action === 'clock-in' ? 'You are checked in. Your time was recorded.' : 'You are checked out. Your time was recorded.');
          this.load();
          if (location) void location.then(position => this.attachLocation(record.id, action, position, attempt));
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(error.error?.detail ?? `Unable to ${action.replace('-', ' ')}.`),
      });
  }

  private attachLocation(recordId: string, action: 'clock-in' | 'clock-out', position: GeolocationPosition | null, attempt: number): void {
    if (!position || !Number.isFinite(position.coords.latitude) || !Number.isFinite(position.coords.longitude) ||
        !Number.isFinite(position.coords.accuracy)) {
      if (attempt === this.locationAttempt) this.locationStatus.set('Attendance time saved. Location was unavailable; your manager can review it.');
      return;
    }
    const latitude = Number(position.coords.latitude.toFixed(7));
    const longitude = Number(position.coords.longitude.toFixed(7));
    const accuracyMeters = Number(position.coords.accuracy.toFixed(2));
    this.api.put<void>(`/me/attendance/${recordId}/location`, { action, latitude, longitude, accuracyMeters }).subscribe({
      next: () => {
        if (attempt !== this.locationAttempt) return;
        this.locationStatus.set(`Location saved. Browser estimated accuracy: approximately ${Math.round(accuracyMeters)} metres.`);
        this.lastLocationUrl.set(`https://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=17/${latitude}/${longitude}`);
      },
      error: () => { if (attempt === this.locationAttempt) this.locationStatus.set('Attendance time saved. Location could not be added; your manager can review it.'); },
    });
  }

  private getLocation(): Promise<GeolocationPosition | null> {
    if (!navigator.geolocation) return Promise.resolve(null);
    return new Promise(resolve => {
      let finished = false;
      const finish = (position: GeolocationPosition | null) => {
        if (finished) return;
        finished = true;
        window.clearTimeout(timer);
        resolve(position);
      };
      const timer = window.setTimeout(() => finish(null), 8_000);
      try {
        navigator.geolocation.getCurrentPosition(
          position => finish(position),
          () => finish(null),
          { enableHighAccuracy: true, maximumAge: 10_000, timeout: 6_000 },
        );
      } catch {
        finish(null);
      }
    });
  }

  private loadProfilePhoto(): void {
    const employeeId = this.auth.user()?.employeeId;
    if (!employeeId) return;
    this.documents.list('Employee', employeeId, 'profile').subscribe({
      next: (items) => {
        const document = items[0];
        if (document) this.loadProfilePhotoContent(document.id);
      },
      error: () => undefined,
    });
  }

  private loadProfilePhotoContent(documentId: string): void {
    this.documents.content(documentId).subscribe({
      next: (blob) => {
        this.revokeProfilePhoto();
        this.profilePhotoUrl.set(URL.createObjectURL(blob));
      },
      error: () => undefined,
    });
  }

  private revokeProfilePhoto(): void {
    const url = this.profilePhotoUrl();
    if (url) URL.revokeObjectURL(url);
    this.profilePhotoUrl.set(null);
  }
}

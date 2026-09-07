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
  readonly dashboard = signal<SelfDashboard | null>(null);
  readonly history = signal<AttendanceRecord[]>([]);
  readonly now = signal(new Date());
  readonly needsCorrection = computed(() => {
    const session = this.dashboard()?.todayAttendance;
    return !!session?.clockedInAt && !session.clockedOutAt && this.now().getTime() - new Date(session.clockedInAt).getTime() > 24 * 60 * 60 * 1000;
  });
  readonly profilePhotoUrl = signal<string | null>(null);
  private clockTimer?: number;

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

  async clock(action: 'clock-in' | 'clock-out'): Promise<void> {
    this.error.set('');
    this.success.set('');
    this.clocking.set(true);
    try {
      let latitude: number | undefined;
      let longitude: number | undefined;
      let accuracyMeters: number | undefined;
      if (this.dashboard()?.requireLocationCapture) {
        const location = await this.getLocation();
        latitude = Number(location.coords.latitude.toFixed(6));
        longitude = Number(location.coords.longitude.toFixed(6));
        accuracyMeters = Number(location.coords.accuracy.toFixed(2));
        this.locationStatus.set(`Location captured within approximately ${Math.round(accuracyMeters)} metres.`);
      }
      this.api
        .post<AttendanceRecord>(`/me/attendance/${action}`, {
          latitude,
          longitude,
          accuracyMeters,
          address: latitude !== undefined && longitude !== undefined ? `${latitude}, ${longitude}` : undefined,
          source: 'web',
        })
        .pipe(finalize(() => this.clocking.set(false)))
        .subscribe({
          next: () => {
            this.success.set(
              action === 'clock-in'
                ? 'You are checked in. Your time and location were recorded.'
                : 'You are checked out. Your time and location were recorded.',
            );
            this.load();
          },
          error: (error: HttpErrorResponse) =>
            this.error.set(error.error?.detail ?? `Unable to ${action.replace('-', ' ')}.`),
        });
    } catch (error) {
      this.clocking.set(false);
      this.error.set(
        error instanceof GeolocationPositionError && error.code === error.PERMISSION_DENIED
          ? 'Location permission is required by your attendance policy. Allow location access and try again.'
          : 'Your location could not be captured. Check device location services and try again.',
      );
      this.locationStatus.set('Location was not recorded.');
    }
  }

  private getLocation(): Promise<GeolocationPosition> {
    if (!navigator.geolocation) return Promise.reject(new Error('Geolocation is unavailable.'));
    return new Promise((resolve, reject) =>
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 15_000,
        maximumAge: 0,
      }),
    );
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

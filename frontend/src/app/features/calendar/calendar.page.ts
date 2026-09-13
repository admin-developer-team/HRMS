import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';

interface Holiday { id: string; name: string; date: string; locationId: string | null; locationName: string | null; isOptional: boolean; appliesToMe: boolean; selected: boolean; version: number }
interface Observance { date: string; name: string; isPublicHoliday: boolean }
interface Leave { startsOn: string; endsOn: string; status: string }
interface Attendance { date: string; status: string; workHours: number }
interface CalendarMonth { year: number; month: number; countryCode: string; locationName: string | null; locations: Location[]; workingDays: string[]; holidays: Holiday[]; observances: Observance[]; leave: Leave[]; attendance: Attendance[]; suggestionsAvailable: boolean }
interface Location { id: string; name: string }
interface DayCell { date: string; day: number; inMonth: boolean; isToday: boolean; isWeekend: boolean; office: Holiday[]; optional: Holiday[]; suggested: Observance[]; leave: Leave[]; attendance: Attendance[] }

@Component({
  selector: 'app-calendar',
  imports: [DatePipe, FormsModule],
  templateUrl: './calendar.page.html',
  styleUrl: './calendar.page.scss',
})
export class CalendarPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  readonly today = this.key(new Date());
  readonly cursor = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly month = signal<CalendarMonth | null>(null);
  readonly locations = signal<Location[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly selectedDate = signal(this.today);
  readonly filterLocationId = signal('');
  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  form = { name: '', date: this.today, locationId: '', isOptional: false, version: 0 };
  readonly canManage = computed(() => this.auth.hasPermission('workforce.manage'));
  readonly title = computed(() => this.cursor().toLocaleDateString('en', { month: 'long', year: 'numeric' }));
  readonly selectedDay = computed(() => this.days().find((d) => d.date === this.selectedDate()));
  readonly officeCount = computed(() => this.month()?.holidays.filter((x) => !x.isOptional && (x.appliesToMe || this.canManage())).length ?? 0);
  readonly optionalCount = computed(() => this.month()?.holidays.filter((x) => x.isOptional && (x.appliesToMe || this.canManage())).length ?? 0);
  readonly publicCount = computed(() => this.month()?.observances.filter((x) => x.isPublicHoliday).length ?? 0);
  readonly days = computed<DayCell[]>(() => {
    const data = this.month();
    const cursor = this.cursor();
    if (!data) return [];
    const start = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
    start.setDate(start.getDate() - start.getDay());
    return Array.from({ length: 42 }, (_, i) => {
      const day = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
      const date = this.key(day);
      const holidays = data.holidays.filter((x) => x.date === date && (x.appliesToMe || this.canManage()));
      return { date, day: day.getDate(), inMonth: day.getMonth() === cursor.getMonth(), isToday: date === this.today,
        isWeekend: !data.workingDays.some((x) => x.toLowerCase() === day.toLocaleDateString('en', { weekday: 'long' }).toLowerCase()),
        office: holidays.filter((x) => !x.isOptional), optional: holidays.filter((x) => x.isOptional),
        suggested: data.observances.filter((x) => x.date === date),
        leave: data.leave.filter((x) => x.startsOn <= date && x.endsOn >= date),
        attendance: data.attendance.filter((x) => x.date === date) };
    });
  });

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const date = params.get('date');
      if (date && /^\d{4}-\d{2}-\d{2}$/.test(date)) {
        const parsed = new Date(`${date}T12:00:00`);
        if (!Number.isNaN(parsed.getTime()) && this.key(parsed) === date) {
          this.cursor.set(new Date(parsed.getFullYear(), parsed.getMonth(), 1));
          this.selectedDate.set(date);
        }
      }
      this.load();
    });
  }

  load(): void {
    const cursor = this.cursor();
    this.loading.set(true);
    this.error.set('');
    this.api.get<CalendarMonth>('/calendar', { year: cursor.getFullYear(), month: cursor.getMonth() + 1, locationId: this.canManage() ? this.filterLocationId() : null }).subscribe({
      next: (data) => { this.month.set(data); this.locations.set(data.locations); this.loading.set(false); },
      error: (e: HttpErrorResponse) => { this.error.set(e.error?.detail ?? 'Calendar could not be loaded.'); this.loading.set(false); },
    });
  }

  move(offset: number): void {
    const current = this.cursor();
    const next = new Date(current.getFullYear(), current.getMonth() + offset, 1);
    this.cursor.set(next);
    this.selectedDate.set(this.key(next));
    this.load();
  }

  jumpToday(): void {
    const now = new Date();
    this.cursor.set(new Date(now.getFullYear(), now.getMonth(), 1));
    this.selectedDate.set(this.today);
    this.load();
  }

  changeLocation(id: string): void { this.filterLocationId.set(id); this.load(); }

  choose(day: DayCell): void { if (day.inMonth) this.selectedDate.set(day.date); }

  openCreate(name = '', date = this.selectedDate()): void {
    this.editingId.set(null);
    this.form = { name, date, locationId: this.filterLocationId(), isOptional: false, version: 0 };
    this.editorOpen.set(true);
  }

  openEdit(holiday: Holiday): void {
    this.editingId.set(holiday.id);
    this.form = { name: holiday.name, date: holiday.date, locationId: holiday.locationId ?? '', isOptional: holiday.isOptional, version: holiday.version };
    this.editorOpen.set(true);
  }

  save(): void {
    const name = this.form.name.trim();
    if (!name || !this.form.date) { this.toast.error('Enter a name and date.'); return; }
    const payload = { ...this.form, name, locationId: this.form.locationId || null };
    const id = this.editingId();
    this.saving.set(true);
    const request = id ? this.api.put<Holiday>(`/workforce/holidays/${id}`, payload) : this.api.post<Holiday>('/workforce/holidays', payload);
    request.subscribe({ next: () => { this.saving.set(false); this.editorOpen.set(false); this.toast.success(id ? 'Holiday updated.' : 'Holiday added.'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.toast.error(e.error?.detail ?? 'Could not save holiday.'); } });
  }

  remove(holiday: Holiday): void {
    if (!window.confirm(`Remove ${holiday.name} from the company calendar?`)) return;
    this.saving.set(true);
    this.api.delete(`/workforce/holidays/${holiday.id}`).subscribe({ next: () => { this.saving.set(false); this.toast.success('Holiday removed.'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.toast.error(e.error?.detail ?? 'Could not remove holiday.'); } });
  }

  select(holiday: Holiday): void {
    this.saving.set(true);
    this.api.put<void>(`/calendar/optional-holidays/${holiday.id}/selection`, { selected: !holiday.selected }).subscribe({
      next: () => { this.saving.set(false); this.toast.success(holiday.selected ? 'Optional holiday removed.' : 'Optional holiday selected.'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.toast.error(e.error?.detail ?? 'Could not update your selection.'); },
    });
  }

  private key(date: Date): string { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; }
}

import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, HostListener, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
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
interface MeetingPerson { employeeId: string; name: string; workEmail: string }
interface Meeting { id: string; title: string; description: string | null; startsAt: string; endsAt: string; location: string | null; meetingUrl: string | null; organizerUserId: string; organizerName: string; attendees: MeetingPerson[]; cancelledAt: string | null; canEdit: boolean; version: number }
interface CalendarMonth { year: number; month: number; countryCode: string; locationName: string | null; locations: Location[]; workingDays: string[]; holidays: Holiday[]; observances: Observance[]; leave: Leave[]; attendance: Attendance[]; meetings: Meeting[]; suggestionsAvailable: boolean }
interface Location { id: string; name: string }
interface DayCell { date: string; day: number; inMonth: boolean; isToday: boolean; isWeekend: boolean; office: Holiday[]; optional: Holiday[]; suggested: Observance[]; leave: Leave[]; attendance: Attendance[]; meetings: Meeting[] }

@Component({
  selector: 'app-calendar',
  imports: [DatePipe, FormsModule],
  templateUrl: './calendar.page.html',
  styleUrl: './calendar.page.scss',
})
export class CalendarPage implements OnInit {
  @ViewChild('addMenu') private addMenu?: ElementRef<HTMLElement>;
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
  readonly meetingEditorOpen = signal(false);
  readonly addMenuOpen = signal(false);
  readonly editingMeetingId = signal<string | null>(null);
  readonly people = signal<MeetingPerson[]>([]);
  readonly peopleSearch = signal('');
  private peopleSearchTimer?: number;
  form = { name: '', date: this.today, locationId: '', isOptional: false, version: 0 };
  meetingForm = { title: '', description: '', startsAt: '', endsAt: '', location: '', meetingUrl: '', attendeeEmployeeIds: [] as string[], version: 0 };
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
        attendance: data.attendance.filter((x) => x.date === date),
        meetings: (data.meetings ?? []).filter(x => !x.cancelledAt && this.key(new Date(x.startsAt)) <= date && this.key(new Date(new Date(x.endsAt).getTime() - 1)) >= date) };
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

  @HostListener('document:click', ['$event'])
  closeAddMenuOnOutsideClick(event: MouseEvent): void {
    if (this.addMenuOpen() && !this.addMenu?.nativeElement.contains(event.target as Node)) this.addMenuOpen.set(false);
  }

  move(offset: number): void {
    const current = this.cursor();
    const next = new Date(current.getFullYear(), current.getMonth() + offset, 1);
    this.cursor.set(next);
    this.selectedDate.set(this.key(next));
    this.addMenuOpen.set(false);
    this.load();
  }

  jumpToday(): void {
    const now = new Date();
    this.cursor.set(new Date(now.getFullYear(), now.getMonth(), 1));
    this.selectedDate.set(this.today);
    this.addMenuOpen.set(false);
    this.load();
  }

  changeLocation(id: string): void { this.filterLocationId.set(id); this.load(); }

  choose(day: DayCell): void { if (day.inMonth) { this.selectedDate.set(day.date); this.addMenuOpen.set(false); } }

  openCreate(name = '', date = this.selectedDate()): void {
    this.addMenuOpen.set(false);
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

  openMeetingCreate(): void {
    this.addMenuOpen.set(false);
    if (this.selectedDate() < this.today) { this.toast.error('Choose today or a future day to schedule a meeting.'); return; }
    this.editingMeetingId.set(null);
    const start = new Date(`${this.selectedDate()}T09:00:00`);
    const now = new Date();
    if (start < now) start.setTime(Math.ceil(now.getTime() / 900_000) * 900_000);
    const end = new Date(start.getTime() + 30 * 60_000);
    this.meetingForm = { title: '', description: '', startsAt: this.localInput(start), endsAt: this.localInput(end),
      location: '', meetingUrl: '', attendeeEmployeeIds: [], version: 0 };
    this.peopleSearch.set('');
    this.loadPeople();
    this.meetingEditorOpen.set(true);
  }

  openMeetingEdit(meeting: Meeting): void {
    this.editingMeetingId.set(meeting.id);
    this.meetingForm = { title: meeting.title, description: meeting.description ?? '',
      startsAt: this.localInput(new Date(meeting.startsAt)), endsAt: this.localInput(new Date(meeting.endsAt)),
      location: meeting.location ?? '', meetingUrl: meeting.meetingUrl ?? '',
      attendeeEmployeeIds: meeting.attendees.map(x => x.employeeId), version: meeting.version };
    this.people.set(meeting.attendees);
    this.peopleSearch.set('');
    this.loadPeople();
    this.meetingEditorOpen.set(true);
  }

  loadPeople(): void {
    if (this.peopleSearchTimer) window.clearTimeout(this.peopleSearchTimer);
    this.peopleSearchTimer = window.setTimeout(() => {
      this.api.get<MeetingPerson[]>('/calendar/meetings/people', { search: this.peopleSearch() }).subscribe({
        next: rows => this.people.set([...this.people().filter(x => this.meetingForm.attendeeEmployeeIds.includes(x.employeeId)),
          ...rows.filter(x => !this.meetingForm.attendeeEmployeeIds.includes(x.employeeId))]),
        error: () => this.toast.error('Could not load meeting attendees.'),
      });
    }, 250);
  }

  toggleAttendee(id: string, selected: boolean): void {
    this.meetingForm.attendeeEmployeeIds = selected
      ? [...new Set([...this.meetingForm.attendeeEmployeeIds, id])]
      : this.meetingForm.attendeeEmployeeIds.filter(x => x !== id);
  }

  attendeeNames(meeting: Meeting): string { return meeting.attendees.map(x => x.name).join(', '); }

  saveMeeting(): void {
    const form = this.meetingForm;
    const start = new Date(form.startsAt);
    const end = new Date(form.endsAt);
    if (form.title.trim().length < 3 || Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end <= start) {
      this.toast.error('Enter a meeting title and a valid start and end time.'); return;
    }
    const payload = { title: form.title.trim(), description: form.description.trim() || null,
      startsAt: start.toISOString(), endsAt: end.toISOString(), location: form.location.trim() || null,
      meetingUrl: form.meetingUrl.trim() || null, attendeeEmployeeIds: form.attendeeEmployeeIds, version: form.version };
    const id = this.editingMeetingId();
    this.saving.set(true);
    const request = id ? this.api.put<Meeting>(`/calendar/meetings/${id}`, payload) : this.api.post<Meeting>('/calendar/meetings', payload);
    request.subscribe({ next: () => { this.saving.set(false); this.meetingEditorOpen.set(false); this.toast.success(id ? 'Meeting updated and attendees notified.' : 'Meeting scheduled and attendees notified.'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.toast.error(e.error?.detail ?? 'Could not save meeting.'); } });
  }

  cancelMeeting(meeting: Meeting): void {
    if (!window.confirm(`Cancel ${meeting.title}? Attendees will be notified.`)) return;
    this.saving.set(true);
    this.api.delete(`/calendar/meetings/${meeting.id}?version=${meeting.version}`).subscribe({
      next: () => { this.saving.set(false); this.toast.success('Meeting cancelled.'); this.load(); },
      error: (e: HttpErrorResponse) => { this.saving.set(false); this.toast.error(e.error?.detail ?? 'Could not cancel meeting.'); },
    });
  }

  private localInput(date: Date): string {
    return `${this.key(date)}T${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
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

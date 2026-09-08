import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  ReactiveFormsModule,
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, combineLatest, finalize, forkJoin, map, of, switchMap, takeUntil } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import {
  ColumnDefinition,
  FormFieldDefinition,
  ModuleDefinition,
  DocumentOwnerType,
  PagedResult,
  RowActionDefinition,
  WorkspaceViewDefinition,
} from '../../core/models';
import { MODULES } from './module.registry';
import { DocumentComponent } from '../../shared/document/document.component';

type DataRow = Record<string, unknown>;
type SelectOption = { label: string; value: string | number | boolean };

@Component({
  selector: 'app-module-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    DocumentComponent,
  ],
  templateUrl: './module.page.html',
  styleUrl: './module.page.scss',
})
export class ModulePage implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(UntypedFormBuilder);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroy$ = new Subject<void>();

  readonly definition = signal<ModuleDefinition>(MODULES['organization']);
  readonly activeTab = signal(0);
  readonly view = computed(
    () => this.definition().views[this.activeTab()] ?? this.definition().views[0],
  );
  readonly rows = signal<DataRow[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = 25;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly drawerOpen = signal(false);
  readonly detailOpen = signal(false);
  readonly locationDialogOpen = signal(false);
  readonly locationRow = signal<DataRow | null>(null);
  readonly documentDialogOpen = signal(false);
  readonly documentOwnerType = signal<DocumentOwnerType>('Employee');
  readonly documentOwnerId = signal('');
  readonly documentCategory = signal('attachment');
  readonly documentLabel = signal('Attachments');
  readonly documentReadonly = signal(false);
  readonly documentMaxFiles = signal(10);
  readonly documentReplaceMode = signal(false);
  readonly documentAllowedExtensions = signal<string[] | undefined>(undefined);
  readonly checkInMapUrl = computed(() => this.mapEmbedUrl(this.locationRow(), 'in'));
  readonly checkOutMapUrl = computed(() => this.mapEmbedUrl(this.locationRow(), 'out'));
  readonly error = signal('');
  readonly success = signal('');
  readonly search = signal('');
  readonly currentAction = signal<RowActionDefinition | null>(null);
  readonly actionRow = signal<DataRow | null>(null);
  readonly detailRows = signal<DataRow[]>([]);
  readonly detailColumns = signal<ColumnDefinition[]>([]);
  readonly detailTitle = signal('Details');
  readonly lookups = signal<Record<string, SelectOption[]>>({});
  readonly lookupLabels = signal<Record<string, string>>({});
  form: UntypedFormGroup = this.fb.group({});
  filterForm: UntypedFormGroup = this.fb.group({});
  @ViewChild('formDocuments') private formDocuments?: DocumentComponent;

  ngOnInit(): void {
    combineLatest([this.route.data, this.route.queryParamMap]).pipe(takeUntil(this.destroy$)).subscribe(([data, query]) => {
      this.definition.set(MODULES[data['module'] as string] ?? MODULES['organization']);
      const requestedView = query.get('view')?.trim().toLocaleLowerCase();
      const viewIndex = requestedView
        ? this.definition().views.findIndex(view => view.label.toLocaleLowerCase() === requestedView)
        : 0;
      this.selectTab(viewIndex >= 0 ? viewIndex : 0);
      if (query.get('action') === 'create' && this.view().createEndpoint) setTimeout(() => {
        this.openCreate();
        void this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { action: null },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      });
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  selectTab(index: number): void {
    this.activeTab.set(index);
    this.search.set('');
    this.page.set(1);
    this.error.set('');
    this.success.set('');
    this.buildFilterForm();
    this.loadLookups();
    this.load(1);
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set('');
    this.page.set(page);
    const view = this.view();
    const params: Record<string, string | number | boolean | undefined> = {};
    Object.entries(this.filterForm.getRawValue() as DataRow).forEach(([key, value]) => {
      if (value === null || value === undefined) return;
      const normalized = typeof value === 'string' ? value.trim() : value;
      if (normalized !== '') params[key] = normalized as string | number | boolean;
    });
    const requiredPathKeys = [...view.endpoint.matchAll(/\{([^}]+)\}/g)].map((match) => match[1]);
    if (requiredPathKeys.some((key) => !params[key])) {
      this.rows.set([]);
      this.total.set(0);
      this.loading.set(false);
      return;
    }
    if (view.listShape === 'paged') {
      params['page'] = page;
      params['pageSize'] = this.pageSize;
      if (this.search()) params['search'] = this.search();
    }
    const endpoint = this.interpolatePath(view.endpoint, null, params);
    this.api.get<unknown>(endpoint, params).subscribe({
      next: (response) => {
        if (view.listShape === 'paged') {
          const result = response as PagedResult<DataRow>;
          this.rows.set(result.items ?? []);
          this.total.set(result.total ?? 0);
        } else {
          const all = Array.isArray(response) ? (response as DataRow[]) : [];
          const query = this.search().trim().toLowerCase();
          const filtered = query
            ? all.filter((row) => JSON.stringify(row).toLowerCase().includes(query))
            : all;
          this.rows.set(filtered);
          this.total.set(filtered.length);
        }
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.errorMessage(error, 'Unable to load this workspace.'));
      },
    });
  }

  applyFilters(): void {
    this.load(1);
  }

  clearFilters(): void {
    const emptyValues = Object.fromEntries(Object.keys(this.filterForm.controls).map((key) => [key, '']));
    this.filterForm.reset(emptyValues);
    this.search.set('');
    this.load(1);
  }

  openCreate(): void {
    const view = this.view();
    if (!view.createEndpoint) return;
    this.openForm(
      {
        label: view.createLabel ?? 'Create record',
        icon: 'add',
        method: 'post',
        path: view.createEndpoint,
        fields: view.fields,
      },
      null,
    );
  }

  openToolbarAction(action: RowActionDefinition): void {
    this.runAction(action, null);
  }

  runRowAction(action: RowActionDefinition, row: DataRow): void {
    this.runAction(action, row);
  }

  actionVisible(action: RowActionDefinition, row: DataRow): boolean {
    if (action.visibleField && action.visibleValues?.length)
      return action.visibleValues.includes(String(this.value(row, action.visibleField) ?? ''));
    if (!action.visibleStatuses?.length) return true;
    return action.visibleStatuses.includes(String(row['status'] ?? ''));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const action = this.currentAction();
    if (!action) return;
    const payload = this.normalizePayload(action.fields ?? [], this.form.getRawValue());
    this.execute(action, this.actionRow(), payload);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    this.currentAction.set(null);
    this.actionRow.set(null);
    this.error.set('');
  }

  closeDetails(): void {
    this.detailOpen.set(false);
    this.detailRows.set([]);
  }

  openAttendanceLocations(row: DataRow): void {
    this.locationRow.set(row);
    this.locationDialogOpen.set(true);
  }

  closeAttendanceLocations(): void {
    this.locationDialogOpen.set(false);
    this.locationRow.set(null);
  }

  closeDocuments(): void {
    this.documentDialogOpen.set(false);
    this.documentOwnerId.set('');
  }

  openColumnDocuments(row: DataRow, column: ColumnDefinition): void {
    const ownerId = String(row['id'] ?? '');
    if (!ownerId || !column.documentOwnerType) return;
    this.documentOwnerType.set(column.documentOwnerType);
    this.documentOwnerId.set(ownerId);
    this.documentCategory.set(column.documentCategory ?? 'attachment');
    this.documentLabel.set(column.documentLabel ?? 'Documents');
    this.documentReadonly.set(Boolean(column.documentReadonly) || Boolean(column.documentReadonlyStatuses?.includes(String(row['status'] ?? ''))));
    this.documentMaxFiles.set(10);
    this.documentReplaceMode.set(false);
    this.documentAllowedExtensions.set(column.documentAllowedExtensions);
    this.documentDialogOpen.set(true);
  }

  hasAttendanceLocation(row: DataRow): boolean {
    return this.coordinates(row, 'in') !== null || this.coordinates(row, 'out') !== null;
  }

  locationText(kind: 'in' | 'out'): string {
    const row = this.locationRow();
    if (!row) return 'Location not captured';
    const address = row[kind === 'in' ? 'clockInAddress' : 'clockOutAddress'];
    const coordinates = this.coordinates(row, kind);
    return typeof address === 'string' && address.trim()
      ? address
      : coordinates
        ? `${coordinates.latitude}, ${coordinates.longitude}`
        : 'Location not captured';
  }

  locationAccuracy(kind: 'in' | 'out'): string {
    const row = this.locationRow();
    const value = row?.[kind === 'in' ? 'clockInAccuracyMeters' : 'clockOutAccuracyMeters'];
    const accuracy = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(accuracy) ? `Accuracy approximately ${Math.round(accuracy)} m` : '';
  }

  exportCsv(): void {
    const columns = this.view().columns;
    const quote = (value: unknown) => `"${this.display(value).replaceAll('"', '""')}"`;
    const lines = [
      columns.map((column) => quote(column.label)).join(','),
      ...this.rows().map((row) =>
        columns.map((column) => quote(this.value(row, column.key))).join(','),
      ),
    ];
    const blob = new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${this.definition().key}-${this.view().label.toLowerCase().replaceAll(' ', '-')}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  optionsFor(
    field: FormFieldDefinition | { key: string; options?: SelectOption[] },
  ): SelectOption[] {
    return this.lookups()[field.key] ?? field.options ?? [];
  }

  value(row: DataRow, key: string): unknown {
    return key
      .split('.')
      .reduce<unknown>(
        (current, part) =>
          current && typeof current === 'object' ? (current as DataRow)[part] : null,
        row,
      );
  }

  dateValue(row: DataRow, key: string): string | number | Date | null {
    const value = this.value(row, key);
    return typeof value === 'string' || typeof value === 'number' || value instanceof Date
      ? value
      : null;
  }

  display(value: unknown, key?: string): string {
    if (value === null || value === undefined || value === '') return '—';
    if (key && typeof value === 'string' && this.lookupLabels()[value])
      return this.lookupLabels()[value];
    if (Array.isArray(value))
      return value.length
        ? value.map((item) => this.lookupLabels()[String(item)] ?? String(item)).join(', ')
        : 'None';
    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }

  statusClass(value: unknown): string {
    const text = String(value).toLowerCase();
    if (
      [
        'active',
        'approved',
        'paid',
        'completed',
        'open',
        'available',
        'verified',
        'present',
        'true',
        'yes',
      ].some((item) => text.includes(item))
    )
      return 'success';
    if (
      [
        'pending',
        'draft',
        'processing',
        'maintenance',
        'probation',
        'submitted',
        'inprogress',
      ].some((item) => text.includes(item))
    )
      return 'warning';
    if (
      ['rejected', 'cancelled', 'terminated', 'inactive', 'suspended', 'lost', 'false', 'no', 'expired'].some((item) =>
        text.includes(item),
      )
    )
      return 'danger';
    return '';
  }

  isOptionSelected(key: string, value: SelectOption['value']): boolean {
    const selected = this.form.get(key)?.value;
    return Array.isArray(selected) && selected.map(String).includes(String(value));
  }

  toggleOption(key: string, value: SelectOption['value']): void {
    const control = this.form.get(key);
    const selected = Array.isArray(control?.value) ? [...control.value] : [];
    const index = selected.findIndex((item) => String(item) === String(value));
    if (index >= 0) selected.splice(index, 1); else selected.push(value);
    control?.setValue(selected);
    control?.markAsTouched();
  }

  multiselectLabel(field: FormFieldDefinition): string {
    const selected = this.form.get(field.key)?.value;
    if (!Array.isArray(selected) || selected.length === 0) return `Select ${field.label.toLowerCase()}`;
    return selected.map((value) => this.lookupLabels()[String(value)] ?? String(value)).join(', ');
  }

  attendanceDuration(): string {
    const start = String(this.form.get('officeStartsAt')?.value ?? '').slice(0, 5);
    const end = String(this.form.get('officeEndsAt')?.value ?? '').slice(0, 5);
    if (!/^\d{2}:\d{2}$/.test(start) || !/^\d{2}:\d{2}$/.test(end)) return '—';
    const [startHour, startMinute] = start.split(':').map(Number);
    const [endHour, endMinute] = end.split(':').map(Number);
    const minutes = endHour * 60 + endMinute - startHour * 60 - startMinute;
    return minutes > 0 ? `${Math.floor(minutes / 60)} h ${minutes % 60} min` : 'End time must be after start time';
  }

  duration(value: unknown): string {
    const hours = Number(value);
    if (!Number.isFinite(hours) || hours <= 0) return '0 min';
    const total = Math.round(hours * 60);
    return `${Math.floor(total / 60) ? `${Math.floor(total / 60)} h ` : ''}${total % 60} min`;
  }

  minutes(value: unknown): string {
    const total = Math.max(0, Math.round(Number(value) || 0));
    return `${Math.floor(total / 60) ? `${Math.floor(total / 60)} h ` : ''}${total % 60} min`;
  }

  private mapEmbedUrl(row: DataRow | null, kind: 'in' | 'out'): SafeResourceUrl | null {
    if (!row) return null;
    const point = this.coordinates(row, kind);
    if (!point) return null;
    const offset = 0.006;
    const bbox = [
      point.longitude - offset,
      point.latitude - offset,
      point.longitude + offset,
      point.latitude + offset,
    ].join(',');
    const url = `https://www.openstreetmap.org/export/embed.html?bbox=${encodeURIComponent(bbox)}&layer=mapnik&marker=${point.latitude}%2C${point.longitude}`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  private coordinates(
    row: DataRow,
    kind: 'in' | 'out',
  ): { latitude: number; longitude: number } | null {
    const rawLatitude = row[kind === 'in' ? 'clockInLatitude' : 'clockOutLatitude'];
    const rawLongitude = row[kind === 'in' ? 'clockInLongitude' : 'clockOutLongitude'];
    if (rawLatitude === null || rawLatitude === undefined || rawLongitude === null || rawLongitude === undefined) return null;
    const latitude = Number(rawLatitude);
    const longitude = Number(rawLongitude);
    return Number.isFinite(latitude) && Number.isFinite(longitude) ? { latitude, longitude } : null;
  }

  actionResultHint(): string {
    const row = this.detailRows()[0];
    return row && Object.keys(row).length === 1 ? String(Object.values(row)[0]) : '';
  }

  private runAction(action: RowActionDefinition, row: DataRow | null): void {
    this.error.set('');
    this.success.set('');
    if (action.method === 'documents') {
      if (!row || !action.documentOwnerType) return;
      const ownerId = String(row[action.documentOwnerIdField ?? 'id'] ?? '');
      if (!ownerId) {
        this.error.set('This record does not have a document owner.');
        return;
      }
      this.documentOwnerType.set(action.documentOwnerType);
      this.documentOwnerId.set(ownerId);
      this.documentCategory.set(action.documentCategory ?? 'attachment');
      this.documentLabel.set(action.documentLabel ?? action.label);
      this.documentReadonly.set(Boolean(action.documentReadonly) || Boolean(action.documentReadonlyStatuses?.includes(String(row['status'] ?? ''))));
      this.documentMaxFiles.set(action.documentMaxFiles ?? 10);
      this.documentReplaceMode.set(action.documentReplaceMode ?? false);
      this.documentAllowedExtensions.set(action.documentAllowedExtensions);
      this.documentDialogOpen.set(true);
      return;
    }
    if (action.method === 'get') {
      this.execute(action, row, {});
      return;
    }
    if (action.fields?.length) {
      this.openForm(action, row);
      return;
    }
    if (action.confirm && !window.confirm(action.confirm)) return;
    this.execute(action, row, {});
  }

  private openForm(action: RowActionDefinition, row: DataRow | null): void {
    this.currentAction.set(action);
    this.actionRow.set(row);
    this.buildForm(action.fields ?? [], row);
    this.loadFieldLookups(action.fields ?? []);
    this.drawerOpen.set(true);
  }

  private execute(action: RowActionDefinition, row: DataRow | null, payload: DataRow): void {
    this.saving.set(true);
    this.error.set('');
    const path = this.interpolatePath(action.path ?? '', row, payload);
    const request =
      action.method === 'get'
        ? this.api.get<unknown>(path)
        : action.method === 'post'
          ? this.api.post<unknown>(path, payload)
          : action.method === 'put'
            ? this.api.put<unknown>(path, payload)
            : this.api.delete<unknown>(path);
    request.pipe(
      switchMap(response => {
        const documents = action.method === 'post' && !row ? this.formDocuments : undefined;
        const ownerId = this.asRow(response)['id'];
        if (!documents || !ownerId) return of(response);
        return documents.saveDocuments(String(ownerId)).pipe(map(() => response));
      }),
      finalize(() => this.saving.set(false)),
    ).subscribe({
      next: (response) => {
        if (action.method === 'get') {
          const resultRows = Array.isArray(response)
            ? (response as DataRow[])
            : [this.asRow(response)];
          this.detailRows.set(resultRows);
          this.detailColumns.set(action.detailColumns ?? this.inferColumns(resultRows[0]));
          this.detailTitle.set(action.detailTitle ?? action.label);
          this.detailOpen.set(true);
          return;
        }
        this.success.set('');
        this.toast.success(`${action.label} completed successfully.`);
        this.closeDrawer();
        this.load(this.page());
      },
      error: (error: HttpErrorResponse) =>
        this.error.set(this.errorMessage(error, `Unable to complete “${action.label}”.`)),
    });
  }

  private interpolatePath(path: string, row: DataRow | null, payload: DataRow): string {
    return path.replace(/\{([^}]+)\}/g, (_, key: string) =>
      encodeURIComponent(String(payload[key] ?? row?.[key] ?? '')),
    );
  }

  private buildForm(fields: FormFieldDefinition[], row: DataRow | null): void {
    const controls: Record<string, unknown> = {};
    for (const field of fields) {
      const source = field.sourceKey ?? field.key;
      const rowValue = row?.[source];
      const initial =
        rowValue ??
        field.defaultValue ??
        (field.type === 'checkbox' ? false : field.type === 'multiselect' ? [] : '');
      const validators = field.required ? [Validators.required] : [];
      if (field.min !== undefined) validators.push(Validators.min(field.min));
      if (field.type === 'email') validators.push(Validators.email);
      if (field.type === 'password') validators.push(Validators.minLength(8));
        const date = field.type === 'datetime-local' && initial ? new Date(String(initial)) : null;
        const value = date && !Number.isNaN(date.getTime())
          ? new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
          : field.type === 'date' && initial ? String(initial).slice(0, 10) : initial;
        controls[field.key] = [value, validators];
    }
    this.form = this.fb.group(controls);
  }

  private buildFilterForm(): void {
    const controls: Record<string, unknown> = {};
    for (const filter of this.view().filters ?? []) controls[filter.key] = [''];
    this.filterForm = this.fb.group(controls);
  }

  private loadLookups(): void {
    const view = this.view();
    const fields = [
      ...(view.fields ?? []),
      ...(view.filters ?? []),
      ...(view.toolbarActions ?? []).flatMap((action) => action.fields ?? []),
      ...(view.rowActions ?? []).flatMap((action) => action.fields ?? []),
    ];
    this.loadFieldLookups(fields);
  }

  private loadFieldLookups(
    fields: Array<
      | FormFieldDefinition
      | {
          key: string;
          optionsEndpoint?: string;
          optionsShape?: 'array' | 'paged';
          optionLabel?: string;
          optionValue?: string;
          options?: SelectOption[];
        }
    >,
  ): void {
    const remote = fields.filter((field) => field.optionsEndpoint);
    const staticLookups = { ...this.lookups() };
    fields.forEach((field) => {
      if (field.options) staticLookups[field.key] = field.options;
    });
    this.lookups.set(staticLookups);
    if (!remote.length) return;
    const unique = [
      ...new Map(remote.map((field) => [`${field.key}:${field.optionsEndpoint}`, field])).values(),
    ];
    forkJoin(unique.map((field) => this.api.get<unknown>(field.optionsEndpoint!).pipe())).subscribe(
      {
        next: (responses) => {
          const next = { ...this.lookups() };
          const labels = { ...this.lookupLabels() };
          unique.forEach((field, index) => {
            const response = responses[index];
            const rows =
              field.optionsShape === 'paged'
                ? ((response as PagedResult<DataRow>).items ?? [])
                : Array.isArray(response)
                  ? (response as DataRow[])
                  : [];
            const labelKey = field.optionLabel ?? 'name';
            const valueKey = field.optionValue ?? 'id';
            const choices = rows.map((row) => ({
              label: String(row[labelKey] ?? row[valueKey]),
              value: String(row[valueKey]),
            }));
            next[field.key] = choices;
            choices.forEach((choice) => (labels[String(choice.value)] = choice.label));
          });
          this.lookups.set(next);
          this.lookupLabels.set(labels);
        },
        error: () => undefined,
      },
    );
  }

  private normalizePayload(fields: FormFieldDefinition[], raw: DataRow): DataRow {
    const payload: DataRow = { ...raw };
    for (const field of fields) {
      const value = payload[field.key];
      if (field.type === 'number')
        payload[field.key] = value === '' || value === null ? null : Number(value);
      if (field.type === 'checkbox') payload[field.key] = Boolean(value);
      if (field.type === 'datetime-local')
        payload[field.key] = value ? new Date(String(value)).toISOString() : null;
      if (field.type === 'multiselect' && !Array.isArray(value))
        payload[field.key] = value ? [value] : [];
      if (value === '') payload[field.key] = null;
    }
    return payload;
  }

  private inferColumns(row?: DataRow): ColumnDefinition[] {
    if (!row) return [];
    return Object.keys(row)
      .slice(0, 8)
      .map((key) => ({ key, label: key.replace(/([a-z])([A-Z])/g, '$1 $2') }));
  }

  private asRow(value: unknown): DataRow {
    if (value && typeof value === 'object') return value as DataRow;
    return { result: value ?? 'Completed' };
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    return (
      error.error?.detail ??
      error.error?.title ??
      (typeof error.error === 'string' ? error.error : fallback)
    );
  }
}

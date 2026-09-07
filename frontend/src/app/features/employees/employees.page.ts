import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { DocumentService } from '../../core/document.service';
import { Employee, PagedResult } from '../../core/models';

interface Lookup {
  id: string;
  name?: string;
  fullName?: string;
}
interface RoleLookup {
  id: string;
  name: string;
  permissions: string[];
}

@Component({
  selector: 'app-employees-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './employees.page.html',
  styleUrl: './employees.page.scss',
})
export class EmployeesPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly documents = inject(DocumentService);
  readonly auth = inject(AuthService);
  readonly data = signal<PagedResult<Employee>>({
    items: [],
    page: 1,
    pageSize: 25,
    total: 0,
    totalPages: 0,
  });
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly drawerOpen = signal(false);
  readonly detailOpen = signal(false);
  readonly accountDrawerOpen = signal(false);
  readonly passwordDrawerOpen = signal(false);
  readonly editing = signal<Employee | null>(null);
  readonly selected = signal<Employee | null>(null);
  readonly accountEmployee = signal<Employee | null>(null);
  readonly passwordEmployee = signal<Employee | null>(null);
  readonly error = signal('');
  readonly success = signal('');
  readonly search = signal('');
  readonly status = signal('');
  readonly departments = signal<Lookup[]>([]);
  readonly designations = signal<Lookup[]>([]);
  readonly locations = signal<Lookup[]>([]);
  readonly managers = signal<Lookup[]>([]);
  readonly roles = signal<RoleLookup[]>([]);
  readonly profilePhoto = signal<File | null>(null);
  readonly profilePhotoPreview = signal<string | null>(null);
  readonly form = this.fb.nonNullable.group({
    employeeNumber: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    workEmail: ['', [Validators.required, Validators.email]],
    phone: [''],
    hireDate: [new Date().toISOString().slice(0, 10), Validators.required],
    employmentType: ['Permanent', Validators.required],
    status: ['Active', Validators.required],
    departmentId: [''],
    designationId: [''],
    locationId: [''],
    managerId: [''],
    baseSalary: [0, [Validators.required, Validators.min(0)]],
    salaryCurrency: ['INR', Validators.required],
    version: [0],
  });
  readonly accountForm = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8)]],
    roleIds: [[] as string[], Validators.required],
  });
  readonly passwordForm = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit(): void {
    this.search.set(this.route.snapshot.queryParamMap.get('search') ?? '');
    this.loadLookups();
    this.load();
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set('');
    this.api
      .employees({ page, pageSize: 25, search: this.search(), status: this.status() })
      .subscribe({
        next: (data) => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(error.error?.detail ?? 'Unable to load employees.');
        },
      });
  }

  openCreate(): void {
    this.clearProfilePhoto();
    this.editing.set(null);
    this.error.set('');
    this.form.reset({
      employeeNumber: '',
      firstName: '',
      lastName: '',
      workEmail: '',
      phone: '',
      hireDate: new Date().toISOString().slice(0, 10),
      employmentType: 'Permanent',
      status: 'Active',
      departmentId: '',
      designationId: '',
      locationId: '',
      managerId: '',
      baseSalary: 0,
      salaryCurrency: 'INR',
      version: 0,
    });
    this.drawerOpen.set(true);
  }

  openEdit(employee: Employee): void {
    this.clearProfilePhoto();
    const parts = employee.fullName.trim().split(/\s+/);
    this.editing.set(employee);
    this.error.set('');
    this.form.reset({
      employeeNumber: employee.employeeNumber,
      firstName: parts.shift() ?? '',
      lastName: parts.join(' '),
      workEmail: employee.workEmail,
      phone: employee.phone ?? '',
      hireDate: employee.hireDate,
      employmentType: employee.employmentType,
      status: employee.status,
      departmentId: employee.departmentId ?? '',
      designationId: employee.designationId ?? '',
      locationId: employee.locationId ?? '',
      managerId: employee.managerId ?? '',
      baseSalary: employee.baseSalary,
      salaryCurrency: employee.salaryCurrency,
      version: employee.version,
    });
    this.drawerOpen.set(true);
  }

  openDetails(employee: Employee): void {
    void this.router.navigate(['/employees', employee.id]);
  }
  openAccount(employee: Employee): void {
    this.accountEmployee.set(employee);
    this.accountForm.reset({ password: '', roleIds: [] });
    this.error.set('');
    this.accountDrawerOpen.set(true);
  }

  openPasswordReset(employee: Employee): void {
    if (!employee.userId) return;
    this.passwordEmployee.set(employee);
    this.passwordForm.reset({ password: '' });
    this.error.set('');
    this.passwordDrawerOpen.set(true);
  }

  provisionAccount(): void {
    if (this.accountForm.invalid || !this.accountEmployee()) {
      this.accountForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.api
      .post(
        `/identity/employees/${this.accountEmployee()!.id}/account`,
        this.accountForm.getRawValue(),
      )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.accountDrawerOpen.set(false);
          this.success.set(`Login account created for ${this.accountEmployee()!.fullName}.`);
          this.load(this.data().page);
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(error.error?.detail ?? 'Unable to create the login account.'),
      });
  }

  resetPassword(): void {
    const employee = this.passwordEmployee();
    if (this.passwordForm.invalid || !employee?.userId) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.api
      .put(`/identity/users/${employee.userId}/password`, this.passwordForm.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.passwordDrawerOpen.set(false);
          this.success.set(`Password reset for ${employee.fullName}.`);
          this.passwordForm.reset({ password: '' });
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(error.error?.detail ?? 'Unable to reset the password.'),
      });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    const raw = this.form.getRawValue();
    const optional = (value: string) => value || null;
    const editing = this.editing();
    const request = editing
      ? this.api.put<Employee>(`/employees/${editing.id}`, {
          firstName: raw.firstName,
          lastName: raw.lastName,
          workEmail: raw.workEmail,
          phone: optional(raw.phone),
          status: raw.status,
          employmentType: raw.employmentType,
          departmentId: optional(raw.departmentId),
          designationId: optional(raw.designationId),
          locationId: optional(raw.locationId),
          managerId: optional(raw.managerId),
          baseSalary: Number(raw.baseSalary),
          salaryCurrency: raw.salaryCurrency,
          version: Number(raw.version),
        })
      : this.api.createEmployee({
          employeeNumber: raw.employeeNumber,
          firstName: raw.firstName,
          lastName: raw.lastName,
          workEmail: raw.workEmail,
          phone: optional(raw.phone),
          hireDate: raw.hireDate,
          employmentType: raw.employmentType,
          departmentId: optional(raw.departmentId),
          designationId: optional(raw.designationId),
          locationId: optional(raw.locationId),
          managerId: optional(raw.managerId),
          baseSalary: Number(raw.baseSalary),
          salaryCurrency: raw.salaryCurrency,
        });
    const selectedPhoto = this.profilePhoto();
    request.pipe(
      switchMap((employee) => !editing && selectedPhoto
        ? this.documents.upload('Employee', employee.id, 'profile', selectedPhoto, true).pipe(map(() => employee))
        : of(employee)),
      finalize(() => this.saving.set(false)),
    ).subscribe({
      next: () => {
        this.drawerOpen.set(false);
        this.clearProfilePhoto();
        this.success.set(
          editing ? 'Employee updated successfully.' : 'Employee created successfully.',
        );
        this.load(this.data().page);
      },
      error: (error: HttpErrorResponse) => {
        if (!editing && selectedPhoto) {
          this.drawerOpen.set(false);
          this.clearProfilePhoto();
          this.error.set(error.error?.detail ?? 'Employee was saved, but the profile photo could not be uploaded.');
          this.load(this.data().page);
          return;
        }
        this.error.set(error.error?.detail ?? `Unable to ${editing ? 'update' : 'create'} employee.`);
      },
    });
  }

  selectProfilePhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp', 'image/avif'].includes(file.type)) {
      this.error.set('Profile photos must be JPG, PNG, WebP, or AVIF images.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.error.set('Profile photos cannot be larger than 5 MB.');
      return;
    }
    this.clearProfilePhoto();
    this.profilePhoto.set(file);
    this.profilePhotoPreview.set(URL.createObjectURL(file));
  }

  removeProfilePhoto(): void {
    this.clearProfilePhoto();
  }

  deleteEmployee(employee: Employee): void {
    if (
      !window.confirm(
        `Delete ${employee.fullName}? This removes the employee from active HR records.`,
      )
    )
      return;
    this.api.delete(`/employees/${employee.id}`).subscribe({
      next: () => {
        this.success.set('Employee deleted successfully.');
        this.load(this.data().page);
      },
      error: (error: HttpErrorResponse) =>
        this.error.set(error.error?.detail ?? 'Unable to delete employee.'),
    });
  }

  exportCsv(): void {
    const header = [
      'Employee ID',
      'Name',
      'Email',
      'Phone',
      'Hire date',
      'Employment type',
      'Status',
      'Base salary',
      'Currency',
    ];
    const quote = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`;
    const lines = [
      header.map(quote).join(','),
      ...this.data().items.map((item) =>
        [
          item.employeeNumber,
          item.fullName,
          item.workEmail,
          item.phone,
          item.hireDate,
          item.employmentType,
          item.status,
          item.baseSalary,
          item.salaryCurrency,
        ]
          .map(quote)
          .join(','),
      ),
    ];
    const url = URL.createObjectURL(
      new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }),
    );
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'employees.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }

  lookup(items: Lookup[], id?: string): string {
    const item = items.find((entry) => entry.id === id);
    return item?.name ?? item?.fullName ?? '—';
  }
  initials(employee: Employee): string {
    return employee.fullName
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }
  statusClass(status: string): string {
    return ['Active', 'Probation'].includes(status)
      ? 'success'
      : status === 'NoticePeriod'
        ? 'warning'
        : 'danger';
  }

  private loadLookups(): void {
    forkJoin({
      departments: this.api.get<Lookup[]>('/organization/departments'),
      designations: this.api.get<Lookup[]>('/organization/designations'),
      locations: this.api.get<Lookup[]>('/organization/locations'),
      employees: this.api.get<PagedResult<Employee>>('/employees', { page: 1, pageSize: 200 }),
    }).subscribe({
      next: (result) => {
        this.departments.set(result.departments);
        this.designations.set(result.designations);
        this.locations.set(result.locations);
        this.managers.set(result.employees.items);
      },
      error: () => undefined,
    });
    if (this.auth.hasPermission('identity.manage'))
      this.api
        .get<RoleLookup[]>('/identity/roles')
        .subscribe({ next: (roles) => this.roles.set(roles) });
  }

  private clearProfilePhoto(): void {
    const preview = this.profilePhotoPreview();
    if (preview) URL.revokeObjectURL(preview);
    this.profilePhoto.set(null);
    this.profilePhotoPreview.set(null);
  }
}

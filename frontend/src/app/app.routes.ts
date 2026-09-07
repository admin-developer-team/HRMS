import { Routes } from '@angular/router';
import { authGuard, employeeGuard, permissionGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
    title: 'Sign in · PeopleFlow HRMS',
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        canActivate: [permissionGuard('dashboard.admin')],
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
        title: 'Dashboard · PeopleFlow',
      },
      {
        path: 'my',
        canActivate: [employeeGuard],
        loadComponent: () =>
          import('./features/self-service/self-dashboard.page').then((m) => m.SelfDashboardPage),
        title: 'My workspace · PeopleFlow',
      },
      {
        path: 'my-services',
        canActivate: [employeeGuard],
        data: { module: 'self' },
        loadComponent: () => import('./features/module/module.page').then((m) => m.ModulePage),
        title: 'My services · PeopleFlow',
      },
      {
        path: 'my-team',
        canActivate: [permissionGuard('team.read')],
        data: { module: 'team' },
        loadComponent: () => import('./features/module/module.page').then((m) => m.ModulePage),
        title: 'My team · PeopleFlow',
      },
      {
        path: 'employees',
        canActivate: [permissionGuard('employees.read')],
        loadComponent: () =>
          import('./features/employees/employees.page').then((m) => m.EmployeesPage),
        title: 'Employees · PeopleFlow',
      },
      {
        path: 'employees/:id',
        canActivate: [permissionGuard('employees.read')],
        loadComponent: () =>
          import('./features/employees/employee-detail.page').then((m) => m.EmployeeDetailPage),
        title: 'Employee profile · PeopleFlow',
      },
      {
        path: 'work',
        canActivate: [permissionGuard('work.read')],
        loadComponent: () => import('./features/work/work.page').then((m) => m.WorkPage),
        title: 'Work management · PeopleFlow',
      },
      {
        path: 'companies',
        canActivate: [permissionGuard('platform.manage')],
        data: { module: 'platform' },
        loadComponent: () => import('./features/module/module.page').then((m) => m.ModulePage),
        title: 'Customer companies · PeopleFlow',
      },
      ...[
        'organization',
        'leave',
        'attendance',
        'workforce',
        'payroll',
        'recruitment',
        'performance',
        'assets',
        'expenses',
        'training',
        'identity',
        'audit',
      ].map((module) => ({
        path: module,
        data: { module },
        canActivate: [
          permissionGuard(
            (
              {
                organization: 'employees.read',
                leave: 'leave.manage',
                attendance: 'attendance.manage',
                workforce: 'workforce.manage',
                payroll: 'payroll.manage',
                recruitment: 'recruitment.manage',
                performance: 'performance.manage',
                assets: 'assets.manage',
                expenses: 'expenses.manage',
                training: 'training.manage',
                identity: 'identity.manage',
                audit: 'audit.read',
              } as Record<string, string>
            )[module],
          ),
        ],
        loadComponent: () => import('./features/module/module.page').then((m) => m.ModulePage),
        title: `${module[0].toUpperCase()}${module.slice(1)} · PeopleFlow`,
      })),
      { path: 'settings/themes', redirectTo: 'settings', pathMatch: 'full' },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/theme-studio.page').then((m) => m.ThemeStudioPage),
        title: 'Settings · PeopleFlow',
      },
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];

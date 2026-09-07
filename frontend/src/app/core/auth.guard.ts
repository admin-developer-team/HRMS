import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

export const permissionGuard =
  (permission: string): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    if (!auth.isAuthenticated()) return inject(Router).createUrlTree(['/login']);
    if (auth.hasPermission(permission)) return true;
    return inject(Router).createUrlTree([auth.isEmployee() ? '/my' : '/dashboard']);
  };

export const employeeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return inject(Router).createUrlTree(['/login']);
  return auth.isEmployee() ? true : inject(Router).createUrlTree(['/dashboard']);
};

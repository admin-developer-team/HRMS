import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

function loginRedirect(router: Router, tenant: string | null, returnUrl: string) {
  return router.createUrlTree(['/login'], {
    queryParams: { ...(tenant ? { tenant } : {}), returnUrl },
  });
}

export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : loginRedirect(inject(Router), route.queryParamMap.get('tenant'), state.url);
};

export const permissionGuard =
  (permission: string): CanActivateFn =>
  (route, state) => {
    const auth = inject(AuthService);
    if (!auth.isAuthenticated()) return loginRedirect(inject(Router), route.queryParamMap.get('tenant'), state.url);
    if (auth.hasPermission(permission)) return true;
    return inject(Router).createUrlTree([auth.isEmployee() ? '/my' : '/dashboard']);
  };

export const employeeGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return loginRedirect(inject(Router), route.queryParamMap.get('tenant'), state.url);
  return auth.isEmployee() ? true : inject(Router).createUrlTree(['/dashboard']);
};

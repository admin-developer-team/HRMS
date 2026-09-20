import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { workspaceSlug } from './workspace-url';

function loginRedirect(router: Router, returnUrl: string) {
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl },
  });
}

export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return loginRedirect(inject(Router), state.url);
  if (auth.session()?.accessPaused && !state.url.startsWith('/access-paused'))
    return inject(Router).createUrlTree(['/access-paused']);
  if (auth.session()?.billingOnly && !state.url.startsWith('/billing'))
    return inject(Router).createUrlTree(['/billing']);
  return true;
};

export const permissionGuard =
  (permission: string): CanActivateFn =>
  (route, state) => {
    const auth = inject(AuthService);
    if (!auth.isAuthenticated()) return loginRedirect(inject(Router), state.url);
    if (auth.hasPermission(permission)) return true;
    return inject(Router).createUrlTree([auth.isEmployee() ? '/my' : workspaceSlug() === 'platform' && auth.hasPermission('support.read') ? '/support' : '/dashboard']);
  };

export const platformSupportGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return loginRedirect(router, state.url);
  return workspaceSlug() === 'platform' && auth.hasPermission('support.read') ? true : router.createUrlTree(['/dashboard']);
};

export const employeeGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) return loginRedirect(inject(Router), state.url);
  return auth.isEmployee() ? true : inject(Router).createUrlTree(['/dashboard']);
};

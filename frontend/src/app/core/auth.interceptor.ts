import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { LoadingService, SKIP_GLOBAL_LOADING } from './loading.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const loading = inject(LoadingService);
  const router = inject(Router);
  const token = auth.accessToken();
  const tenantId = auth.tenantId();

  const secured = token
    ? request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`,
          ...(tenantId !== 'anonymous' ? { 'X-Tenant-ID': tenantId } : {}),
        },
      })
    : request;

  const showGlobalLoading = request.method === 'GET' && !request.context.get(SKIP_GLOBAL_LOADING);
  if (showGlobalLoading) loading.begin();
  return next(secured).pipe(
    catchError((error: HttpErrorResponse) => {
      if (
        error.status === 401 &&
        !request.url.endsWith('/auth/login') &&
        !request.url.endsWith('/auth/refresh')
      ) {
        return auth.refreshSession().pipe(
          switchMap((session) =>
            next(
              request.clone({
                setHeaders: {
                  Authorization: `Bearer ${session.accessToken}`,
                  'X-Tenant-ID': session.user.tenantId,
                },
              }),
            ),
          ),
          catchError((refreshError) => {
            auth.logout(false);
            void router.navigate(['/login']);
            return throwError(() => refreshError);
          }),
        );
      }
      if (error.status === 401 && request.url.endsWith('/auth/refresh')) {
        auth.logout(false);
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
    finalize(() => showGlobalLoading && loading.end()),
  );
};

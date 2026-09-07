import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Dashboard, Employee, PagedResult, UserSession } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  login(payload: { tenantSlug: string; email: string; password: string }): Observable<UserSession> {
    return this.http.post<UserSession>(`${this.baseUrl}/auth/login`, payload);
  }

  refresh(refreshToken: string, tenantId: string): Observable<UserSession> {
    return this.http.post<UserSession>(
      `${this.baseUrl}/auth/refresh`,
      { refreshToken },
      { headers: { 'X-Tenant-ID': tenantId } },
    );
  }

  dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.baseUrl}/dashboard`);
  }

  employees(query: {
    page: number;
    pageSize: number;
    search?: string;
    status?: string;
  }): Observable<PagedResult<Employee>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.search) params = params.set('search', query.search);
    if (query.status) params = params.set('status', query.status);
    return this.http.get<PagedResult<Employee>>(`${this.baseUrl}/employees`, { params });
  }

  createEmployee(payload: Record<string, unknown>): Observable<Employee> {
    return this.http.post<Employee>(`${this.baseUrl}/employees`, payload);
  }

  get<T>(
    path: string,
    params?: Record<string, string | number | boolean | null | undefined>,
  ): Observable<T> {
    let httpParams = new HttpParams();
    Object.entries(params ?? {}).forEach(([key, value]) => {
      if (value === undefined || value === null) return;
      const normalized = typeof value === 'string' ? value.trim() : value;
      if (normalized !== '') httpParams = httpParams.set(key, String(normalized));
    });
    return this.http.get<T>(`${this.baseUrl}${path}`, { params: httpParams });
  }

  post<T>(path: string, payload: unknown): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, payload);
  }

  put<T>(path: string, payload: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, payload);
  }

  delete<T = void>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`);
  }
}

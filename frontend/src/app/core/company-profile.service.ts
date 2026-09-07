import { Injectable, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { ApiService } from './api.service';
import { CompanyProfile } from './models';

@Injectable({ providedIn: 'root' })
export class CompanyProfileService {
  private readonly api = inject(ApiService);
  readonly profile = signal<CompanyProfile | null>(null);

  load() { return this.api.get<CompanyProfile>('/company-profile').pipe(tap((value) => this.profile.set(value))); }
  update(payload: Omit<CompanyProfile, 'id' | 'slug' | 'logoDocumentId'>) {
    return this.api.put<CompanyProfile>('/company-profile', payload).pipe(tap((value) => this.profile.set(value)));
  }
}

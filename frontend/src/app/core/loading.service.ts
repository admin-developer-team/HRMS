import { HttpContextToken } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';

export const SKIP_GLOBAL_LOADING = new HttpContextToken<boolean>(() => false);

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private pending = 0;
  private showTimer?: ReturnType<typeof setTimeout>;
  private hideTimer?: ReturnType<typeof setTimeout>;
  readonly visible = signal(false);

  begin(): void {
    this.pending++;
    if (this.pending !== 1) return;
    clearTimeout(this.hideTimer);
    this.showTimer = setTimeout(() => this.visible.set(true), 140);
  }

  end(): void {
    this.pending = Math.max(0, this.pending - 1);
    if (this.pending) return;
    clearTimeout(this.showTimer);
    this.hideTimer = setTimeout(() => this.visible.set(false), 120);
  }
}

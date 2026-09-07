import { Injectable, signal } from '@angular/core';

export type ToastTone = 'success' | 'error' | 'info';
export interface ToastMessage { id: number; text: string; tone: ToastTone }

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  readonly messages = signal<ToastMessage[]>([]);

  success(text: string): void { this.show(text, 'success'); }
  error(text: string): void { this.show(text, 'error', 5000); }
  info(text: string): void { this.show(text, 'info'); }
  dismiss(id: number): void { this.messages.update((items) => items.filter((item) => item.id !== id)); }

  private show(text: string, tone: ToastTone, duration = 3500): void {
    const id = ++this.nextId;
    this.messages.update((items) => [...items.slice(-3), { id, text, tone }]);
    setTimeout(() => this.dismiss(id), duration);
  }
}

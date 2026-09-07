import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { SKIP_GLOBAL_LOADING } from './loading.service';
import { NotificationFeed } from './models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${environment.apiUrl}/notifications`;
  private readonly hubUrl = environment.notificationHubUrl;
  private readonly quiet = new HttpContext().set(SKIP_GLOBAL_LOADING, true);
  private connection: HubConnection | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  readonly feed = signal<NotificationFeed>({ items: [], unreadCount: 0 });

  connect(): void {
    if (!this.auth.accessToken() || this.connection) return;
    const connection = new HubConnectionBuilder()
      .withUrl(this.hubUrl, { accessTokenFactory: () => this.auth.accessToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    this.connection = connection;
    connection.on('NotificationsChanged', () => this.refreshQuietly());
    connection.onreconnected(() => this.refreshQuietly());
    connection.onclose(() => {
      if (this.connection !== connection) return;
      this.connection = null;
      this.scheduleReconnect();
    });
    void connection.start().catch(() => {
      if (this.connection === connection) this.connection = null;
      this.scheduleReconnect();
    });
  }

  disconnect(): void {
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
    this.reconnectTimer = null;
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) void connection.stop();
  }

  load() {
    return this.http.get<NotificationFeed>(this.url, { context: this.quiet }).pipe(tap((feed) => this.feed.set(feed)));
  }

  markRead(id: string) {
    return this.http.put<void>(`${this.url}/${id}/read`, {}).pipe(tap(() => this.feed.update((feed) => ({
      unreadCount: Math.max(0, feed.unreadCount - (feed.items.find((item) => item.id === id)?.isRead ? 0 : 1)),
      items: feed.items.map((item) => item.id === id ? { ...item, isRead: true } : item),
    }))));
  }

  markAllRead() {
    return this.http.put<void>(`${this.url}/read-all`, {}).pipe(tap(() => this.feed.update((feed) => ({
      unreadCount: 0, items: feed.items.map((item) => ({ ...item, isRead: true })),
    }))));
  }

  private refreshQuietly(): void {
    this.load().subscribe({ error: () => undefined });
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer || !this.auth.accessToken()) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.connect();
    }, 5000);
  }
}

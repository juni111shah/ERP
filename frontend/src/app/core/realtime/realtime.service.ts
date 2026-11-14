import { Injectable, NgZone, computed, effect, inject, signal, untracked } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { API_BASE_URL } from '../api/api-config';
import { SessionStore } from '../session/session.store';
import { AccessSync } from './access-sync.service';

/** What the server sends with "access-changed": the workspace it happened in and why (role-changed, modules-changed...). */
export interface AccessChangedEvent { orgId: string; reason: string; }

/** The hub next to the versioned API root: "/api/v1" → "/hubs/notifications", "https://x/api/v1" → "https://x/hubs/notifications". */
export function hubUrl(apiBase: string): string {
  return apiBase.replace(/\/api\/v\d+\/?$/, '') + '/hubs/notifications';
}

/**
 * The signed-in person's live connection to the server (SignalR). It is open while there is a session and carries two events:
 * "notification" (a new in-app notification: the bell count is asked again) and "access-changed" (an administrator changed this
 * person's roles, a role they hold, or the workspace's modules: the session is refreshed and the menu, buttons and guards follow).
 * The SignalR client is loaded only once someone is signed in, so the sign-in page and the public site stay small.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly session = inject(SessionStore);
  private readonly access = inject(AccessSync);
  private readonly zone = inject(NgZone);
  private readonly url = hubUrl(inject(API_BASE_URL));
  private connection: HubConnection | null = null;
  /** Bumped on every start and stop, so a connection that finishes starting after a sign-out is closed rather than kept. */
  private generation = 0;
  private readonly signedIn = computed(() => !!this.session.accessToken() && !!this.session.me());

  /** Bumped by every "notification" event, so the unread count can be asked again at once. */
  readonly notificationTick = signal(0);
  /** The connection is up (for diagnostics and tests). */
  readonly connected = signal(false);

  constructor() {
    effect(() => {
      const on = this.signedIn();
      untracked(() => (on ? void this.start() : this.stop()));
    });
  }

  private async start(): Promise<void> {
    if (this.connection || typeof window === 'undefined') return;
    const generation = ++this.generation;
    let signalr: typeof import('@microsoft/signalr');
    try { signalr = await import('@microsoft/signalr'); } catch { return; }
    if (generation !== this.generation || this.connection) return;
    const connection = new signalr.HubConnectionBuilder()
      // Read at every (re)connect, so a refreshed token is used after a drop.
      .withUrl(this.url, { accessTokenFactory: () => this.session.accessToken() ?? '' })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(signalr.LogLevel.None)
      .build();
    connection.on('notification', () => this.zone.run(() => this.notificationTick.update((n) => n + 1)));
    connection.on('access-changed', (e: AccessChangedEvent) => this.zone.run(() => this.onAccessChanged(e)));
    // Changes made while the connection was down were missed: check the session once it is back.
    connection.onreconnected(() => this.zone.run(() => { this.connected.set(true); this.access.refresh('reconnected'); }));
    connection.onreconnecting(() => this.zone.run(() => this.connected.set(false)));
    connection.onclose(() => this.zone.run(() => { this.connected.set(false); if (this.connection === connection) this.connection = null; }));
    this.connection = connection;
    try {
      await connection.start();
      this.zone.run(() => this.connected.set(true));
    } catch {
      // The server may be restarting or a proxy may block the hub: the app works without it (the API still enforces every permission).
      if (this.connection === connection) this.connection = null;
    }
  }

  private stop(): void {
    this.generation++;
    const connection = this.connection;
    this.connection = null;
    this.connected.set(false);
    if (connection) void connection.stop().catch(() => undefined);
  }

  private onAccessChanged(e: AccessChangedEvent): void {
    // The event is per person, not per workspace: a change in another workspace of theirs does not touch this tab's session.
    const workspace = this.session.me()?.workspace.id;
    if (e?.orgId && workspace && e.orgId.toLowerCase() !== workspace.toLowerCase()) return;
    this.access.refresh(e?.reason ?? 'access-changed');
  }
}

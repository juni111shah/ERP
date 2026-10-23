import { TranslatePipe } from '../core/i18n/translate.pipe';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal, effect } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { API_BASE_URL } from '../core/api/api-config';

interface NotificationDto { id: string; title: string; body?: string; link?: string; severity: string; createdAt: string; readAt?: string; }

/** Notification centre: latest notifications, open the linked record (marks it read), or mark everything read. */
@Component({
  selector: 'erp-notification-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, NzDrawerModule, NzButtonModule],
  styles: [`
    .item { padding: 10px 0; border-bottom: 1px solid var(--erp-color-border-subtle); cursor: pointer; }
    .item.unread .title { font-weight: 600; }
    .meta { font-size: 12px; color: var(--erp-color-text-tertiary); }
    .empty { color: var(--erp-color-text-tertiary); padding: 24px 0; text-align: center; }
  `],
  template: `
    <nz-drawer [nzVisible]="open()" [nzTitle]="'shell.notifications' | translate: 'Notifications'" nzWidth="420" (nzOnClose)="closed.emit()">
      <ng-container *nzDrawerContent>
        <button nz-button nzSize="small" (click)="markAll()" data-testid="mark-all-read">{{ 'shell.markAllRead' | translate: 'Mark all as read' }}</button>
        @for (n of items(); track n.id) {
          <div class="item" [class.unread]="!n.readAt" (click)="openItem(n)" data-testid="notification-item">
            <div class="title">{{ n.title }}</div>
            @if (n.body) { <div>{{ n.body }}</div> }
            <div class="meta">{{ n.createdAt | date: 'yyyy-MM-dd HH:mm' }}</div>
          </div>
        } @empty { <div class="empty">{{ 'shell.caughtUp' | translate: 'All caught up' }}</div> }
      </ng-container>
    </nz-drawer>`,
})
export class NotificationDrawerComponent {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);
  readonly open = input.required<boolean>();
  readonly closed = output<void>();
  readonly changed = output<void>();
  readonly items = signal<NotificationDto[]>([]);

  constructor() { effect(() => { if (this.open()) this.load(); }); }

  private load(): void { this.http.get<{ items: NotificationDto[] }>(`${this.base}/notifications`).subscribe((r) => this.items.set(r.items)); }

  markAll(): void { this.http.post(`${this.base}/notifications/read-all`, {}).subscribe(() => { this.load(); this.changed.emit(); }); }

  openItem(n: NotificationDto): void {
    const go = () => { this.closed.emit(); if (n.link) void this.router.navigateByUrl(n.link); };
    if (n.readAt) { go(); return; }
    this.http.post(`${this.base}/notifications/${n.id}/read`, {}).subscribe({ next: () => { this.changed.emit(); go(); }, error: go });
  }
}

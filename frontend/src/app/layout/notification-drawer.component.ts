import { TranslatePipe } from '../core/i18n/translate.pipe';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal, effect, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { API_BASE_URL } from '../core/api/api-config';

interface NotificationDto {
  id: string;
  title: string;
  body?: string;
  link?: string;
  severity: string;
  createdAt: string;
  readAt?: string;
}

/** Notification dropdown menu: opens directly under bell icon, displays latest notifications, mark read, or quick links. */
@Component({
  selector: 'erp-notification-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, RouterLink, NzButtonModule, NzIconModule],
  styles: [`
    :host {
      display: block;
    }
    .notifications-dropdown-panel {
      background: var(--erp-card-bg, #ffffff);
      border-radius: 12px;
      border: 1px solid var(--erp-border, #e5e7eb);
      box-shadow: 0 12px 36px rgba(0, 0, 0, 0.16), 0 4px 12px rgba(0, 0, 0, 0.08);
      width: 380px;
      max-width: 92vw;
      overflow: hidden;
      display: flex;
      flex-direction: column;
    }
    :host-context([data-theme='dark']) .notifications-dropdown-panel {
      background: #0f172a;
      border-color: #334155;
      box-shadow: 0 16px 40px rgba(0, 0, 0, 0.45);
    }

    /* Header */
    .notif-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      border-bottom: 1px solid var(--erp-border-subtle, #f1f5f9);
      background: var(--erp-header-bg, #ffffff);
    }
    :host-context([data-theme='dark']) .notif-header {
      border-color: #1e293b;
      background: #0f172a;
    }
    .notif-header-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      font-weight: 700;
      color: var(--erp-text, #0f172a);
    }
    :host-context([data-theme='dark']) .notif-header-title {
      color: #f8fafc;
    }
    .notif-bell-icon {
      color: var(--erp-brand-600, #10955d);
      font-size: 16px;
    }
    .notif-badge {
      background: rgba(16, 149, 93, 0.12);
      color: var(--erp-brand-600, #10955d);
      font-size: 11px;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: 10px;
      line-height: 1.2;
    }
    :host-context([data-theme='dark']) .notif-badge {
      background: rgba(16, 149, 93, 0.25);
      color: #34d399;
    }
    .notif-mark-all {
      font-size: 12px;
      color: var(--erp-brand-600, #10955d);
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 2px 6px;
      border-radius: 4px;
      cursor: pointer;
    }
    .notif-mark-all:hover {
      background: rgba(16, 149, 93, 0.08);
    }

    /* List container */
    .notif-list {
      max-height: 380px;
      overflow-y: auto;
      overscroll-behavior: contain;
    }
    .notif-list::-webkit-scrollbar {
      width: 5px;
    }
    .notif-list::-webkit-scrollbar-thumb {
      background: rgba(0, 0, 0, 0.15);
      border-radius: 3px;
    }
    :host-context([data-theme='dark']) .notif-list::-webkit-scrollbar-thumb {
      background: rgba(255, 255, 255, 0.2);
    }

    /* Item */
    .notif-item {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      border-bottom: 1px solid var(--erp-border-subtle, #f8fafc);
      cursor: pointer;
      position: relative;
      transition: background 0.15s ease;
    }
    :host-context([data-theme='dark']) .notif-item {
      border-color: #1e293b;
    }
    .notif-item:hover {
      background: var(--erp-hover-bg, #f8fafc);
    }
    :host-context([data-theme='dark']) .notif-item:hover {
      background: #1e293b;
    }
    .notif-item.unread {
      background: rgba(16, 149, 93, 0.035);
    }
    :host-context([data-theme='dark']) .notif-item.unread {
      background: rgba(16, 149, 93, 0.08);
    }
    .notif-item-icon {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: #f1f5f9;
      color: #64748b;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 15px;
      flex-shrink: 0;
      margin-top: 2px;
    }
    :host-context([data-theme='dark']) .notif-item-icon {
      background: #1e293b;
      color: #94a3b8;
    }
    .notif-item-icon.unread {
      background: rgba(16, 149, 93, 0.1);
      color: var(--erp-brand-600, #10955d);
    }
    :host-context([data-theme='dark']) .notif-item-icon.unread {
      background: rgba(16, 149, 93, 0.2);
      color: #34d399;
    }
    .notif-item-content {
      flex: 1;
      min-width: 0;
    }
    .notif-item-title {
      font-size: 13px;
      color: var(--erp-text, #1e293b);
      line-height: 1.4;
      word-break: break-word;
    }
    :host-context([data-theme='dark']) .notif-item-title {
      color: #e2e8f0;
    }
    .notif-item.unread .notif-item-title {
      font-weight: 600;
      color: var(--erp-text, #0f172a);
    }
    :host-context([data-theme='dark']) .notif-item.unread .notif-item-title {
      color: #f8fafc;
    }
    .notif-item-body {
      font-size: 12px;
      color: var(--erp-text-secondary, #64748b);
      margin-top: 2px;
      line-height: 1.4;
      overflow: hidden;
      text-overflow: ellipsis;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }
    :host-context([data-theme='dark']) .notif-item-body {
      color: #94a3b8;
    }
    .notif-item-time {
      font-size: 11px;
      color: var(--erp-text-muted, #94a3b8);
      margin-top: 4px;
    }
    :host-context([data-theme='dark']) .notif-item-time {
      color: #64748b;
    }
    .notif-unread-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--erp-brand-600, #10955d);
      flex-shrink: 0;
      margin-top: 6px;
    }

    /* Empty State */
    .notif-empty {
      padding: 36px 20px;
      text-align: center;
      color: var(--erp-text-secondary, #64748b);
    }
    .notif-empty-icon {
      font-size: 32px;
      color: var(--erp-brand-600, #10955d);
      margin-bottom: 8px;
      opacity: 0.85;
    }
    .notif-empty-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--erp-text, #1e293b);
      margin-bottom: 4px;
    }
    :host-context([data-theme='dark']) .notif-empty-title {
      color: #e2e8f0;
    }
    .notif-empty-desc {
      font-size: 12px;
      color: var(--erp-text-muted, #94a3b8);
    }

    /* Footer */
    .notif-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 16px;
      background: var(--erp-bg-subtle, #f8fafc);
      border-top: 1px solid var(--erp-border-subtle, #f1f5f9);
    }
    :host-context([data-theme='dark']) .notif-footer {
      background: #0b1120;
      border-color: #1e293b;
    }
    .notif-footer-link {
      font-size: 12px;
      color: var(--erp-brand-600, #10955d);
      display: inline-flex;
      align-items: center;
      gap: 5px;
      text-decoration: none;
      font-weight: 500;
      transition: opacity 0.15s;
    }
    .notif-footer-link:hover {
      opacity: 0.8;
      text-decoration: underline;
    }
  `],
  template: `
    <div class="notifications-dropdown-panel" data-testid="notifications-menu">
      <!-- Header -->
      <div class="notif-header">
        <div class="notif-header-title">
          <span nz-icon nzType="bell" class="notif-bell-icon"></span>
          <span>{{ 'shell.notifications' | translate: 'Notifications' }}</span>
          @if (unreadCount() > 0) {
            <span class="notif-badge">{{ unreadCount() }}</span>
          }
        </div>
        @if (items().length > 0) {
          <button nz-button nzType="text" nzSize="small" (click)="markAll()" class="notif-mark-all" data-testid="mark-all-read">
            <span nz-icon nzType="check"></span>
            {{ 'shell.markAllRead' | translate: 'Mark all as read' }}
          </button>
        }
      </div>

      <!-- Items list -->
      <div class="notif-list">
        @for (n of items(); track n.id) {
          <div class="notif-item" [class.unread]="!n.readAt" (click)="openItem(n)" data-testid="notification-item">
            <div class="notif-item-icon" [class.unread]="!n.readAt">
              <span nz-icon [nzType]="getIcon(n.severity)" nzTheme="outline"></span>
            </div>
            <div class="notif-item-content">
              <div class="notif-item-title">{{ n.title }}</div>
              @if (n.body) {
                <div class="notif-item-body">{{ n.body }}</div>
              }
              <div class="notif-item-time">{{ formatTime(n.createdAt) }}</div>
            </div>
            @if (!n.readAt) {
              <div class="notif-unread-dot" title="Unread"></div>
            }
          </div>
        } @empty {
          <div class="notif-empty">
            <div class="notif-empty-icon">
              <span nz-icon nzType="check-circle" nzTheme="outline"></span>
            </div>
            <div class="notif-empty-title">{{ 'shell.caughtUp' | translate: 'All caught up' }}</div>
            <div class="notif-empty-desc">{{ 'shell.noNotifications' | translate: 'No new notifications right now' }}</div>
          </div>
        }
      </div>

      <!-- Footer -->
      <div class="notif-footer">
        <a routerLink="/approvals" (click)="closed.emit()" class="notif-footer-link">
          <span nz-icon nzType="check-square"></span>
          {{ 'core.approvals' | translate: 'Approvals' }}
        </a>
        <a routerLink="/admin/audit" (click)="closed.emit()" class="notif-footer-link">
          <span nz-icon nzType="history"></span>
          {{ 'core.audit' | translate: 'Audit Log' }}
        </a>
      </div>
    </div>
  `,
})
export class NotificationDrawerComponent {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);

  readonly open = input.required<boolean>();
  readonly closed = output<void>();
  readonly changed = output<void>();
  readonly items = signal<NotificationDto[]>([]);

  readonly unreadCount = computed(() => this.items().filter((x) => !x.readAt).length);

  constructor() {
    effect(() => {
      if (this.open()) this.load();
    });
  }

  private load(): void {
    this.http.get<{ items: NotificationDto[] }>(`${this.base}/notifications`).subscribe({
      next: (r) => this.items.set(r.items || []),
      error: () => this.items.set([])
    });
  }

  markAll(): void {
    this.http.post(`${this.base}/notifications/read-all`, {}).subscribe({
      next: () => {
        this.load();
        this.changed.emit();
      }
    });
  }

  openItem(n: NotificationDto): void {
    const go = () => {
      this.closed.emit();
      if (n.link) void this.router.navigateByUrl(n.link);
    };
    if (n.readAt) {
      go();
      return;
    }
    this.http.post(`${this.base}/notifications/${n.id}/read`, {}).subscribe({
      next: () => {
        this.changed.emit();
        go();
      },
      error: go
    });
  }

  getIcon(severity?: string): string {
    switch ((severity || '').toLowerCase()) {
      case 'success': return 'check-circle';
      case 'warning': return 'exclamation-circle';
      case 'error':
      case 'danger': return 'close-circle';
      default: return 'bell';
    }
  }

  formatTime(dateStr: string): string {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMin = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMin / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMin < 1) return 'Just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
}

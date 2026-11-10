import { TranslatePipe } from '@core/i18n/translate.pipe';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal, effect, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { API_BASE_URL } from '@core/api/api-config';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';

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
  imports: [KeyboardClickDirective, TranslatePipe, DatePipe, RouterLink, NzButtonModule, NzIconModule],
  styleUrl: './notification-drawer.component.scss',
  templateUrl: './notification-drawer.component.html',
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

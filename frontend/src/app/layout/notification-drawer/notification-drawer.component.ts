import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NotificationDto, NotificationsApi } from '@core/api/notifications.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { notificationAge, notificationIcon } from './notification-format';

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
  private readonly notificationsApi = inject(NotificationsApi);
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

  markAll(): void {
    this.notificationsApi.markAllRead().subscribe({
      next: () => {
        this.load();
        this.changed.emit();
      },
    });
  }

  /** Marks an unread notification read (whatever the answer), then closes the menu and follows its link. */
  openItem(n: NotificationDto): void {
    const go = () => {
      this.closed.emit();
      if (n.link) void this.router.navigateByUrl(n.link);
    };
    if (n.readAt) {
      go();
      return;
    }
    this.notificationsApi.markRead(n.id).subscribe({
      next: () => {
        this.changed.emit();
        go();
      },
      error: go,
    });
  }

  getIcon(severity?: string): string { return notificationIcon(severity); }
  formatTime(dateStr: string): string { return notificationAge(dateStr); }

  private load(): void {
    this.notificationsApi.inbox().subscribe({
      next: (r) => this.items.set(r.items || []),
      error: () => this.items.set([]),
    });
  }
}

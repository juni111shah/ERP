import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, combineLatest, interval, map, of, startWith, switchMap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/session/session.store';
import { KNOWN_ICONS } from '../icons';
import { GlobalSearchComponent } from './global-search.component';
import { NotificationDrawerComponent } from './notification-drawer.component';

interface MenuGroup { id: string; label: string; order: number; items: { id: string; label: string; route: string; icon?: string; module: string }[]; }

/** App shell: white sidebar with the server-driven menu, header with workspace/entity switchers, notifications, theme and user menu. */
@Component({
  selector: 'erp-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NzLayoutModule, NzMenuModule, NzIconModule, NzButtonModule,
    NzDropDownModule, NzAvatarModule, NzBadgeModule, NzSelectModule, NzInputModule, NzToolTipModule, GlobalSearchComponent, NotificationDrawerComponent],
  styles: [`
    :host { display: block; height: 100vh; }
    nz-layout.root { height: 100vh; }
    .sider { background: var(--erp-color-nav-bg); border-inline-end: 1px solid var(--erp-color-nav-border); overflow: auto; }
    .brand { height: 56px; display: flex; align-items: center; gap: 8px; padding: 0 20px; font-weight: 600; font-size: 16px; color: var(--erp-color-primary); }
    .brand-mark { width: 28px; height: 28px; border-radius: 8px; background: var(--erp-color-primary); color: var(--erp-color-on-primary); display: grid; place-items: center; font-size: 13px; }
    .header { background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); display: flex; align-items: center; gap: 12px; padding: 0 16px; height: 56px; line-height: 56px; }
    .spacer { flex: 1; }
    .search { max-width: 360px; }
    .content { padding: 24px; overflow: auto; background: var(--erp-color-bg-canvas); }
    .group-title { padding: 16px 20px 4px; font-size: 12px; color: var(--erp-color-text-tertiary); text-transform: uppercase; letter-spacing: .04em; }
  `],
  template: `
    <nz-layout class="root">
      <nz-sider class="sider" nzCollapsible [nzCollapsed]="collapsed()" [nzTrigger]="null" nzWidth="248px" [nzCollapsedWidth]="64" nzTheme="light" data-testid="sidebar">
        <div class="brand"><span class="brand-mark">E</span>@if (!collapsed()) { <span>{{ workspaceName() }}</span> }</div>
        @for (g of menu(); track g.id) {
          @if (!collapsed()) { <div class="group-title">{{ g.label }}</div> }
          <ul nz-menu nzMode="inline" [nzInlineCollapsed]="collapsed()">
            @for (i of g.items; track i.id) {
              <li nz-menu-item nz-tooltip [nzTooltipTitle]="collapsed() ? i.label : null" nzTooltipPlacement="right"
                  [routerLink]="i.route" routerLinkActive="ant-menu-item-selected" [routerLinkActiveOptions]="{ exact: i.route === '/' }"
                  [attr.data-testid]="'nav-' + i.id">
                <span nz-icon [nzType]="icon(i.icon)"></span><span>{{ i.label }}</span>
              </li>
            }
          </ul>
        }
      </nz-sider>
      <nz-layout>
        <nz-header class="header">
          <button nz-button nzType="text" (click)="collapsed.set(!collapsed())" aria-label="Toggle navigation" data-testid="nav-toggle">
            <span nz-icon [nzType]="collapsed() ? 'menu-unfold' : 'menu-fold'"></span>
          </button>
          <erp-global-search />
          <span class="spacer"></span>
          @if (session.me(); as me) {
            @if (me.legalEntities.length > 1 || me.allLegalEntities) {
              <nz-select style="width: 220px" [ngModel]="session.currentEntityId()" (ngModelChange)="selectEntity($event)" data-testid="entity-switcher">
                @if (me.allLegalEntities) { <nz-option [nzValue]="null" nzLabel="All legal entities"></nz-option> }
                @for (e of me.legalEntities; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }
              </nz-select>
            }
            @if (me.workspaces.length > 1) {
              <button nz-button nz-dropdown [nzDropdownMenu]="wsMenu" data-testid="workspace-switcher"><span nz-icon nzType="swap"></span>{{ me.workspace.name }}</button>
              <nz-dropdown-menu #wsMenu="nzDropdownMenu">
                <ul nz-menu>
                  @for (w of me.workspaces; track w.id) {
                    <li nz-menu-item [nzDisabled]="w.id === me.workspace.id" (click)="switchWorkspace(w.id)" [attr.data-testid]="'workspace-option-' + w.code">{{ w.name }}</li>
                  }
                </ul>
              </nz-dropdown-menu>
            }
          }
          <button nz-button nzType="text" aria-label="Notifications" data-testid="notifications-bell" (click)="notificationsOpen.set(true)">
            <nz-badge [nzCount]="unread().unread" nzSize="small"><span nz-icon nzType="bell"></span></nz-badge>
          </button>
          <button nz-button nzType="text" (click)="theme.toggleTheme()" aria-label="Toggle dark mode" data-testid="theme-toggle"><span nz-icon nzType="bulb"></span></button>
          <button nz-button nzType="text" nz-dropdown [nzDropdownMenu]="userMenu" data-testid="user-menu">
            <nz-avatar nzSize="small" [nzText]="initials()"></nz-avatar>
          </button>
          <nz-dropdown-menu #userMenu="nzDropdownMenu">
            <ul nz-menu>
              <li nz-menu-item nzDisabled>{{ session.me()?.user?.email }}</li>
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="auth.logout()" data-testid="sign-out"><span nz-icon nzType="logout"></span> Sign out</li>
            </ul>
          </nz-dropdown-menu>
        </nz-header>
        <nz-content class="content"><router-outlet /></nz-content>
        <erp-notification-drawer [open]="notificationsOpen()" (closed)="notificationsOpen.set(false)" (changed)="bump()" />
      </nz-layout>
    </nz-layout>`,
})
export class ShellComponent {
  readonly session = inject(SessionStore);
  readonly auth = inject(AuthService);
  readonly theme = inject(TenantThemeService);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);

  readonly collapsed = signal(false);
  readonly notificationsOpen = signal(false);
  readonly refresh = signal(0);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());

  // Menu reloads whenever the session changes (workspace switch, module enabled/disabled).
  readonly menu = toSignal(toObservable(this.session.me).pipe(
    switchMap((me) => (me ? this.http.get<MenuGroup[]>(`${this.base}/navigation/menu`).pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as MenuGroup[] });

  // Unread count: on session change, after the drawer changes something, and every minute.
  readonly unread = toSignal(combineLatest([toObservable(this.session.me), toObservable(this.refresh), interval(60_000).pipe(startWith(0))]).pipe(
    map(([me]) => me),
    switchMap((me) => (me ? this.http.get<{ unread: number }>(`${this.base}/notifications`).pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
  ), { initialValue: { unread: 0 } }) as () => { unread: number };

  bump(): void { this.refresh.update((n) => n + 1); }

  icon(name?: string): string {
    if (!name) return 'appstore';
    const full = name.replace(/(^|-)([a-z])/g, (_, d, c) => c.toUpperCase()) + 'Outline';
    return KNOWN_ICONS.has(full) ? name : 'appstore';
  }

  selectEntity(id: string | null): void {
    this.session.setEntity(id);
    // Reload the current screen so lists re-query in the new scope.
    const url = this.router.url;
    void this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => this.router.navigateByUrl(url));
  }

  switchWorkspace(id: string): void {
    this.auth.switchWorkspace(id).subscribe(() => void this.router.navigateByUrl('/'));
  }
}

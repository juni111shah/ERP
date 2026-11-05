import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PlatformApi } from '../core/api/platform.api';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/session/session.store';
import { BrandLogoComponent } from '../shared/brand-logo.component';

/** What a suspended or cancelled workspace sees instead of its pages: why, that the data is safe, and who to talk to. */
@Component({
  selector: 'erp-workspace-blocked',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, BrandLogoComponent],
  styles: [`
    :host { display: grid; place-items: center; min-height: 100vh; padding: 24px; box-sizing: border-box; background: var(--erp-color-bg-canvas); overflow-y: auto; }
    .card { width: 100%; max-width: 520px; padding: 32px; box-sizing: border-box; text-align: center; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 16px; box-shadow: 0 4px 20px -2px rgba(0,0,0,.05); }
    .brand { display: flex; justify-content: center; margin-bottom: 20px; }
    .brand erp-brand-logo { width: auto; }
    .icon { width: 56px; height: 56px; margin: 0 auto 14px; display: grid; place-items: center; border-radius: 50%; font-size: 26px; background: var(--erp-color-error-bg); color: var(--erp-color-error-text); }
    h1 { margin: 0 0 8px; font-size: 20px; font-weight: 700; color: var(--erp-color-text-primary); }
    p { margin: 0 0 12px; color: var(--erp-color-text-muted); line-height: 1.5; overflow-wrap: anywhere; }
    .reason { padding: 10px 14px; border-radius: 8px; background: var(--erp-color-bg-subtle); color: var(--erp-color-text-primary); }
    .actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; margin-top: 16px; }
    .switch { margin-top: 16px; padding-top: 12px; border-top: 1px solid var(--erp-color-border-subtle); }
    @media (max-width: 767px) { :host { padding: 16px; } .card { padding: 20px 16px; } }
  `],
  template: `
    <div class="card" data-testid="workspace-blocked">
      <div class="brand"><erp-brand-logo size="lg"></erp-brand-logo></div>
      <div class="icon" aria-hidden="true"><span nz-icon nzType="lock"></span></div>
      <h1 data-testid="workspace-blocked-title">{{ cancelled() ? 'This subscription is cancelled' : 'This workspace is suspended' }}</h1>
      <p>{{ workspace() }} is not available right now. Your data is kept and comes back as soon as access is restored.</p>
      @if (reason(); as r) { <p class="reason" data-testid="workspace-blocked-reason">{{ r }}</p> }
      @if (supportEmail(); as mail) { <p>To sort this out, write to <a [href]="'mailto:' + mail" data-testid="workspace-blocked-support">{{ mail }}</a>.</p> }
      @else if (!canSeeBilling()) { <p>Please ask your administrator to contact us.</p> }
      <div class="actions">
        @if (canSeeBilling()) { <a nz-button nzType="primary" routerLink="/admin/billing" data-testid="workspace-blocked-billing"><span nz-icon nzType="credit-card"></span>Subscription and billing</a> }
        <button nz-button type="button" [nzLoading]="checking()" (click)="check()" data-testid="workspace-blocked-check"><span nz-icon nzType="reload"></span>Check again</button>
        @if (session.me()?.platformAdmin) { <a nz-button routerLink="/platform">Platform console</a> }
        <button nz-button type="button" (click)="auth.logout()" data-testid="workspace-blocked-sign-out"><span nz-icon nzType="logout"></span>Sign out</button>
      </div>
      @if (others().length) {
        <div class="switch">
          <p>Your other workspaces:</p>
          <div class="actions">@for (w of others(); track w.id) { <button nz-button nzSize="small" type="button" (click)="switchTo(w.id)" [attr.data-testid]="'workspace-blocked-switch-' + w.code">{{ w.name }}</button> }</div>
        </div>
      }
    </div>`,
})
export class WorkspaceBlockedComponent {
  readonly session = inject(SessionStore);
  readonly auth = inject(AuthService);
  private readonly api = inject(PlatformApi);
  private readonly router = inject(Router);

  readonly supportEmail = signal<string | null>(null);
  readonly checking = signal(false);
  readonly workspace = computed(() => this.session.me()?.workspace.name ?? 'This workspace');
  readonly cancelled = computed(() => this.session.me()?.subscription?.status === 'cancelled');
  readonly reason = computed(() => this.session.me()?.subscription?.suspendedReason ?? null);
  readonly canSeeBilling = computed(() => this.session.hasPermission('core.billing.read'));
  readonly others = computed(() => { const me = this.session.me(); return (me?.workspaces ?? []).filter((w) => w.id !== me?.workspace.id); });

  constructor() {
    if (this.canSeeBilling()) this.api.billing().subscribe({ next: (b) => this.supportEmail.set(b.supportEmail), error: () => undefined });
  }

  check(): void {
    this.checking.set(true);
    this.auth.loadMe().subscribe({ next: () => this.checking.set(false), error: () => this.checking.set(false) });
  }

  switchTo(id: string): void {
    this.auth.switchWorkspace(id).subscribe(() => void this.router.navigateByUrl('/'));
  }
}

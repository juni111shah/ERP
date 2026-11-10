import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PlatformApi } from '@core/api/platform.api';
import { AuthService } from '@core/auth/auth.service';
import { SessionStore } from '@core/session/session.store';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';

/** What a suspended or cancelled workspace sees instead of its pages: why, that the data is safe, and who to talk to. */
@Component({
  selector: 'erp-workspace-blocked',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, BrandLogoComponent],
  styleUrl: './workspace-blocked.component.scss',
  templateUrl: './workspace-blocked.component.html',
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

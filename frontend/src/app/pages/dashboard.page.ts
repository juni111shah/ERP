import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { SessionStore } from '../core/session/session.store';

interface QuickLink { label: string; route: string; permission: string; }

const QUICK_LINKS: QuickLink[] = [
  { label: 'Parties', route: '/parties', permission: 'core.parties.read' },
  { label: 'Custom fields', route: '/admin/custom-fields', permission: 'core.customfields.read' },
  { label: 'Modules', route: '/admin/modules', permission: 'core.settings.read' },
];

@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzCardModule, NzTagModule],
  styles: [`.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 16px; } h1 { margin: 0 0 4px; }`],
  template: `
    <h1 data-testid="dashboard-title">Welcome, {{ firstName() }}</h1>
    <p>{{ workspace() }} · {{ currency() }}</p>
    <div class="grid">
      @for (l of links(); track l.route) {
        <nz-card [nzTitle]="l.label"><a [routerLink]="l.route">Open</a></nz-card>
      }
      <nz-card nzTitle="Enabled modules">
        @for (m of modules(); track m) { <nz-tag>{{ m }}</nz-tag> }
      </nz-card>
    </div>`,
})
export class DashboardPage {
  private readonly session = inject(SessionStore);
  readonly firstName = computed(() => (this.session.me()?.user.fullName ?? '').split(' ')[0]);
  readonly workspace = computed(() => this.session.me()?.workspace.name ?? '');
  readonly currency = computed(() => this.session.me()?.workspace.baseCurrency ?? '');
  readonly modules = computed(() => [...this.session.enabledModules()]);
  readonly links = computed(() => QUICK_LINKS.filter((l) => this.session.hasPermission(l.permission)));
}

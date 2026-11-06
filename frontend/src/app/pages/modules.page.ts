import { switchMap } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';
import { StatePanelComponent } from '../shared/state-panel.component';

interface ModuleView { key: string; name: string; description: string; category: string; version: string; isCore: boolean; requires: string[]; uiRoute: string; state: 'enabled' | 'disabled' | 'unavailable'; }

/** Workspace modules: core is always on; industry and functional modules can be enabled or disabled (data is kept). */
@Component({
  selector: 'erp-modules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, NzCardModule, NzGridModule, NzTagModule, NzButtonModule, HasPermissionDirective],
  styles: [`
    .module-card {
      background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow);
      border-radius: 14px;
      display: flex;
      flex-direction: column;
      padding: 20px;
      height: 100%;
    }
    .head { display:flex; justify-content:space-between; align-items:flex-start; gap:8px; margin-bottom:10px }
    .mod-title { font-size:16px; font-weight:700; color: var(--erp-color-text-primary); }
    .mod-desc { color: var(--erp-color-text-muted); font-size:13.5px; line-height:1.5; flex:1; min-height: 44px; margin-bottom:16px }
    .group-title { font-size: 15px; font-weight: 600; margin: 24px 0 12px; color: var(--erp-color-text-primary); }
    .mod-requires { margin: -8px 0 12px; font-size: 12.5px; color: var(--erp-color-text-muted); }
    .action-row { margin-top: auto; display: flex; align-items: center; justify-content: space-between; }
  `],
  template: `
    <h1 class="erp-page-title">Modules</h1>
    <p style="color:var(--erp-color-text-muted); margin-top:-8px; margin-bottom:20px;">Turn on the parts of the system your business needs. Each module shows what it needs switched on first.</p>
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the modules" hint="The server did not return the list." actionLabel="Try again" (action)="load()" />
    }
    @for (g of groups(); track g.category) {
      <h2 class="group-title" [attr.data-testid]="'module-group-' + g.category">{{ g.category }}</h2>
      <div nz-row [nzGutter]="[16, 16]" class="group-row">
      @for (m of g.modules; track m.key) {
        <div nz-col [nzXs]="24" [nzMd]="12" [nzXl]="8" [attr.data-testid]="'module-row-' + m.key">
          <div class="module-card">
            <div class="head">
              <span class="mod-title">{{ m.name }}</span>
              <nz-tag [nzColor]="m.state === 'enabled' ? 'green' : m.state === 'disabled' ? 'default' : 'blue'">
                {{ m.isCore ? 'Always on' : m.state === 'unavailable' ? 'Coming soon' : m.state === 'enabled' ? 'Enabled' : 'Disabled' }}
              </nz-tag>
            </div>
            <p class="mod-desc">{{ m.description }}</p>
            @if (m.requires.length) {
              <p class="mod-requires" [attr.data-testid]="'module-requires-' + m.key">Needs: {{ requiredNames(m) }}</p>
            }
            <div class="action-row">
              <span style="font-size:12px; color:var(--erp-color-text-muted); font-weight:500;">v{{ m.version || '1.0' }}</span>
              @if (!m.isCore && m.state !== 'unavailable') {
                <button *erpHasPermission="'core.modules.manage'" nz-button [nzType]="m.state === 'enabled' ? 'default' : 'primary'" [nzLoading]="busy() === m.key"
                        (click)="toggle(m)" [attr.data-testid]="'module-toggle-' + m.key">{{ m.state === 'enabled' ? 'Disable' : 'Enable module' }}</button>
              }
            </div>
          </div>
        </div>
      }
      </div>
    }`,
})
export class ModulesPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  readonly modules = signal<ModuleView[]>([]);
  /** The modules under their category, in the order the server lists them. */
  readonly groups = computed(() => {
    const byCategory = new Map<string, ModuleView[]>();
    for (const m of this.modules()) byCategory.set(m.category || 'Other', [...(byCategory.get(m.category || 'Other') ?? []), m]);
    return [...byCategory].map(([category, modules]) => ({ category, modules }));
  });
  /** The names of the modules a module needs switched on first. */
  requiredNames(m: ModuleView): string {
    return m.requires.map((key) => this.modules().find((x) => x.key === key)?.name ?? key).join(', ');
  }
  readonly busy = signal<string | null>(null);
  readonly failed = signal(false);

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    this.http.get<ModuleView[]>(`${this.base}/modules`).subscribe({ next: (m) => this.modules.set(m), error: () => this.failed.set(true) });
  }

  toggle(m: ModuleView): void {
    const action = m.state === 'enabled' ? 'disable' : 'enable';
    this.busy.set(m.key);
    this.http.post(`${this.base}/modules/${m.key}/${action}`, {}).subscribe({
      next: () => {
        this.busy.set(null);
        this.message.success(`${m.name} ${action}d`);
        this.load();
        // Permissions travel in the sign-in token, so a newly enabled module needs a fresh one before its calls are allowed.
        this.auth.refresh().pipe(switchMap(() => this.auth.loadMe())).subscribe(); // refresh menu and module guards
      },
      error: (e) => { this.busy.set(null); this.message.error(asProblem(e)?.detail ?? 'Could not change the module'); },
    });
  }
}

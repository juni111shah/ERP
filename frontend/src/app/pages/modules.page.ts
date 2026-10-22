import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';

interface ModuleView { key: string; name: string; description: string; category: string; version: string; isCore: boolean; requires: string[]; uiRoute: string; state: 'enabled' | 'disabled' | 'unavailable'; }

/** Workspace modules: core is always on; industry and functional modules can be enabled or disabled (data is kept). */
@Component({
  selector: 'erp-modules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzCardModule, NzGridModule, NzTagModule, NzButtonModule, HasPermissionDirective],
  styles: [`nz-card { height: 100%; } .head { display:flex; justify-content:space-between; align-items:center; gap:8px } p { color: var(--erp-color-text-secondary); min-height: 44px }`],
  template: `
    <h1 class="erp-page-title">Modules</h1>
    <div nz-row [nzGutter]="[16, 16]">
      @for (m of modules(); track m.key) {
        <div nz-col [nzXs]="24" [nzMd]="12" [nzXl]="8" [attr.data-testid]="'module-row-' + m.key">
          <nz-card>
            <div class="head"><strong>{{ m.name }}</strong>
              <nz-tag [nzColor]="m.state === 'enabled' ? 'green' : m.state === 'disabled' ? 'default' : 'blue'">{{ m.isCore ? 'Always on' : m.state === 'unavailable' ? 'Coming soon' : m.state === 'enabled' ? 'Enabled' : 'Disabled' }}</nz-tag>
            </div>
            <p>{{ m.description }}</p>
            @if (!m.isCore && m.state !== 'unavailable') {
              <button *erpHasPermission="'core.settings.write'" nz-button [nzType]="m.state === 'enabled' ? 'default' : 'primary'" [nzLoading]="busy() === m.key"
                      (click)="toggle(m)" [attr.data-testid]="'module-toggle-' + m.key">{{ m.state === 'enabled' ? 'Disable' : 'Enable' }}</button>
            }
          </nz-card>
        </div>
      }
    </div>`,
})
export class ModulesPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  readonly modules = signal<ModuleView[]>([]);
  readonly busy = signal<string | null>(null);

  constructor() { this.load(); }

  load(): void { this.http.get<ModuleView[]>(`${this.base}/modules`).subscribe((m) => this.modules.set(m)); }

  toggle(m: ModuleView): void {
    const action = m.state === 'enabled' ? 'disable' : 'enable';
    this.busy.set(m.key);
    this.http.post(`${this.base}/modules/${m.key}/${action}`, {}).subscribe({
      next: () => {
        this.busy.set(null);
        this.message.success(`${m.name} ${action}d`);
        this.load();
        this.auth.loadMe().subscribe(); // refresh menu and module guards
      },
      error: (e) => { this.busy.set(null); this.message.error(asProblem(e)?.detail ?? 'Could not change the module'); },
    });
  }
}

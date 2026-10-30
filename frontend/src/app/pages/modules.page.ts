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
  styles: [`
    .module-card {
      border-radius: 14px;
      border: 1px solid #e2e8f0;
      background: #ffffff;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
      display: flex;
      flex-direction: column;
      padding: 20px;
      height: 100%;
    }
    .module-card:hover {
      box-shadow: 0 10px 25px -5px rgba(16, 149, 93, 0.12);
      border-color: #6ee7b7;
      transform: translateY(-2px);
    }
    .head { display:flex; justify-content:space-between; align-items:flex-start; gap:8px; margin-bottom:10px }
    .mod-title { font-size:16px; font-weight:700; color:#0f172a; }
    .mod-desc { color: #64748b; font-size:13.5px; line-height:1.5; flex:1; min-height: 44px; margin-bottom:16px }
    .action-row { margin-top: auto; display: flex; align-items: center; justify-content: space-between; }
  `],
  template: `
    <h1 class="erp-page-title">Modules</h1>
    <p style="color:#64748b; margin-top:-8px; margin-bottom:20px;">Extend your workspace with industry-specific modules and functional packs.</p>
    <div nz-row [nzGutter]="[16, 16]">
      @for (m of modules(); track m.key) {
        <div nz-col [nzXs]="24" [nzMd]="12" [nzXl]="8" [attr.data-testid]="'module-row-' + m.key">
          <div class="module-card">
            <div class="head">
              <span class="mod-title">{{ m.name }}</span>
              <nz-tag [nzColor]="m.state === 'enabled' ? 'green' : m.state === 'disabled' ? 'default' : 'blue'">
                {{ m.isCore ? 'Always on' : m.state === 'unavailable' ? 'Coming soon' : m.state === 'enabled' ? 'Enabled' : 'Disabled' }}
              </nz-tag>
            </div>
            <p class="mod-desc">{{ m.description }}</p>
            <div class="action-row">
              <span style="font-size:12px; color:#94a3b8; font-weight:500;">v{{ m.version || '1.0' }}</span>
              @if (!m.isCore && m.state !== 'unavailable') {
                <button *erpHasPermission="'core.settings.write'" nz-button [nzType]="m.state === 'enabled' ? 'default' : 'primary'" [nzLoading]="busy() === m.key"
                        (click)="toggle(m)" [attr.data-testid]="'module-toggle-' + m.key">{{ m.state === 'enabled' ? 'Disable' : 'Enable module' }}</button>
              }
            </div>
          </div>
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

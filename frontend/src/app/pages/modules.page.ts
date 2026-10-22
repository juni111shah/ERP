import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';

interface ModuleView {
  key: string; name: string; description: string; category: string; version: string;
  isCore: boolean; requires: string[]; uiRoute: string; state: 'enabled' | 'disabled' | 'unavailable';
}

/** Workspace module catalog: enable or disable installed modules. Disabling keeps the module's data. */
@Component({
  selector: 'erp-modules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzCardModule, NzButtonModule, NzTagModule, HasPermissionDirective],
  styles: [`.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; } p { min-height: 44px; }`],
  template: `
    <h1>Modules</h1>
    <div class="grid" data-testid="modules-grid">
      @for (m of modules(); track m.key) {
        <nz-card [nzTitle]="m.name" [nzExtra]="stateTag" [attr.data-testid]="'module-' + m.key">
          <p>{{ m.description }}</p>
          <small>{{ m.category }} · v{{ m.version }}@if (m.requires.length) { · needs {{ m.requires.join(', ') }} }</small>
          <ng-template #stateTag><nz-tag [nzColor]="m.state === 'enabled' ? 'green' : 'default'">{{ m.state }}</nz-tag></ng-template>
          @if (!m.isCore && m.state !== 'unavailable') {
            <div *erpHasPermission="'core.modules.manage'" style="margin-top:12px">
              <button nz-button [nzType]="m.state === 'enabled' ? 'default' : 'primary'" [nzLoading]="busy() === m.key"
                      (click)="toggle(m)" [attr.data-testid]="'toggle-' + m.key">
                {{ m.state === 'enabled' ? 'Disable' : 'Enable' }}
              </button>
            </div>
          }
        </nz-card>
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

  toggle(m: ModuleView): void {
    const action = m.state === 'enabled' ? 'disable' : 'enable';
    this.busy.set(m.key);
    this.http.post(`${this.base}/modules/${m.key}/${action}`, {}).subscribe({
      next: () => {
        this.busy.set(null);
        this.message.success(`${m.name} ${action}d`);
        this.load();
        this.auth.loadMe().subscribe(); // refresh enabled modules and the server-driven menu
      },
      error: (e) => { this.busy.set(null); this.message.error(asProblem(e)?.title ?? 'Could not change the module'); },
    });
  }

  private load(): void {
    this.http.get<ModuleView[]>(`${this.base}/modules`).subscribe((m) => this.modules.set(m));
  }
}

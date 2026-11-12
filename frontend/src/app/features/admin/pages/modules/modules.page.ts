import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { switchMap } from 'rxjs';
import { ModulesApi, WorkspaceModule } from '@core/api/modules.api';
import { AuthService } from '@core/auth/auth.service';
import { asProblem } from '@core/http/problem-details';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { modulesByCategory, requiredModuleNames } from '../../admin-modules';

/** Workspace modules: core is always on; industry and functional modules can be enabled or disabled (data is kept). */
@Component({
  selector: 'erp-modules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, NzCardModule, NzGridModule, NzTagModule, NzButtonModule, HasPermissionDirective],
  styleUrl: './modules.page.scss',
  templateUrl: './modules.page.html',
})
export class ModulesPage {
  private readonly api = inject(ModulesApi);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  readonly modules = signal<WorkspaceModule[]>([]);
  /** The modules under their category, in the order the server lists them. */
  readonly groups = computed(() => modulesByCategory(this.modules()));
  readonly busy = signal<string | null>(null);
  readonly failed = signal(false);

  constructor() { this.load(); }

  /** The names of the modules a module needs switched on first. */
  requiredNames(m: WorkspaceModule): string { return requiredModuleNames(m, this.modules()); }

  load(): void {
    this.failed.set(false);
    this.api.list().subscribe({ next: (m) => this.modules.set(m), error: () => this.failed.set(true) });
  }

  toggle(m: WorkspaceModule): void {
    const action = m.state === 'enabled' ? 'disable' : 'enable';
    this.busy.set(m.key);
    this.api.setState(m.key, action).subscribe({
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

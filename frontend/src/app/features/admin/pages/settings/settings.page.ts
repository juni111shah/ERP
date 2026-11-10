import { doneText } from '@core/format/done-text';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AdminApi, SettingView } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CoaNumberingEditorComponent } from '@features/finance/coa/components/coa-numbering-editor/coa-numbering-editor.component';

const MODULE_NAMES: Record<string, string> = { hr: 'HR and payroll', realestate: 'Real estate', crm: 'CRM', pos: 'Point of sale' };

/** Workspace settings grouped by module. Every setting is declared by core or a module; this page only renders them by type. */
@Component({
  selector: 'erp-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSpinModule, StatePanelComponent, CoaNumberingEditorComponent, HasPermissionDirective, NzButtonModule, NzInputModule, NzInputNumberModule, NzSwitchModule],
  styleUrl: './settings.page.scss',
  templateUrl: './settings.page.html',
})
export class SettingsPage {
  private readonly api = inject(AdminApi);
  private readonly message = inject(NzMessageService);
  readonly settings = signal<SettingView[]>([]);
  readonly draft = signal<Record<string, unknown>>({});
  readonly saving = signal(false);
  readonly loading = signal(true);
  readonly failed = signal(false);
  private readonly original = signal<Record<string, unknown>>({});
  readonly groups = computed(() => {
    const byModule = new Map<string, SettingView[]>();
    for (const s of this.settings().filter((x) => x.type !== 'json')) byModule.set(s.module, [...(byModule.get(s.module) ?? []), s]);
    return [...byModule].map(([module, items]) => ({ module, items }));
  });
  readonly dirty = computed(() => Object.keys(this.draft()).some((k) => this.draft()[k] !== this.original()[k]));

  constructor() { this.load(); }

  set(key: string, value: unknown): void { this.draft.update((d) => ({ ...d, [key]: value })); }

  save(): void {
    const changed: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(this.draft())) if (v !== this.original()[k]) changed[k] = v;
    this.saving.set(true);
    this.api.saveSettings(changed).subscribe({
      next: () => { this.saving.set(false); this.message.success(doneText('saved', 'Workspace settings')); this.load(); },
      error: (e) => { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the settings')); },
    });
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.settings().subscribe({
      next: (list) => {
        const values = Object.fromEntries(list.map((s) => [s.key, s.value]));
        this.settings.set(list);
        this.original.set(values);
        this.draft.set({ ...values });
        this.loading.set(false);
      },
      error: () => { this.failed.set(true); this.loading.set(false); },
    });
  }

  moduleName(module: string): string {
    return module === 'core' ? 'General' : MODULE_NAMES[module] ?? displayLabel(module);
  }
}

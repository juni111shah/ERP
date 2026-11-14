import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { AdminApi, SettingView } from '@core/api/admin.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { CoaNumberingEditorComponent } from '@features/finance/coa/components/coa-numbering-editor/coa-numbering-editor.component';
import { changedValues, groupSettings, settingsModuleName } from '../../admin-settings';

/** Workspace settings grouped by module. Every setting is declared by core or a module; this page only renders them by type. */
@Component({
  selector: 'erp-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSpinModule, StatePanelComponent, CoaNumberingEditorComponent, NzButtonModule, NzInputModule, NzInputNumberModule, NzSwitchModule],
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
  readonly groups = computed(() => groupSettings(this.settings()));
  readonly dirty = computed(() => Object.keys(this.draft()).some((k) => this.draft()[k] !== this.original()[k]));
  private readonly session = inject(SessionStore);
  /** Save shows when at least one listed group may be written. */
  readonly canSaveAny = computed(() => this.groups().some((g) => this.canWrite(g.module)));

  /** As the server decides: core keys need core.settings.write, a module's keys that or the module's own `<module>.settings.write`. */
  canWrite(module: string): boolean {
    return this.session.hasPermission('core.settings.write') || (module !== 'core' && this.session.hasPermission(`${module}.settings.write`));
  }

  constructor() { this.load(); }

  set(key: string, value: unknown): void { this.draft.update((d) => ({ ...d, [key]: value })); }

  /** Saves only the settings that were changed. */
  save(): void {
    const changed = Object.fromEntries(Object.entries(changedValues(this.draft(), this.original())).filter(([key]) => this.canWrite(key.split('.')[0])));
    if (!Object.keys(changed).length) return;
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

  moduleName(module: string): string { return settingsModuleName(module); }
}

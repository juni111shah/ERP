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
  styles: [`
    .wrap { max-width: 740px; display:flex; flex-direction:column; gap:24px; }
    .group-card { background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 12px; padding: 20px 24px; }
    .group-card h3 { font-size: 16px; font-weight: 700; color: var(--erp-color-text-primary); margin: 0 0 14px; border-bottom: 1px solid var(--erp-color-border-subtle); padding-bottom: 10px; }
    .row { display:flex; gap:16px; align-items:center; padding:12px 0; border-bottom:1px solid var(--erp-color-border-subtle) }
    .row:last-child { border-bottom: none; padding-bottom: 0; }
    .label { flex:1; font-weight: 500; color: var(--erp-color-text-primary); font-size: 14px; }
    .label small { display:block; color: var(--erp-color-text-muted); font-weight: normal; font-size: 12.5px; margin-top: 2px; }
    .actions { display:flex; justify-content:flex-end; }
    .loading { text-align:center; padding:48px; }
  `],
  template: `
    <h1 class="erp-page-title">Settings</h1>
    <div class="wrap">
      @if (loading()) {
        <div class="loading"><nz-spin nzSimple></nz-spin></div>
      } @else if (failed()) {
        <erp-state-panel kind="error" title="Could not load the workspace settings" hint="The server could not read the settings list, so they cannot be changed right now. Your saved values are not affected." actionLabel="Try again" (action)="load()" data-testid="settings-error" />
      } @else {
        @for (g of groups(); track g.module) {
          <section class="group-card">
            <h3>{{ moduleName(g.module) }}</h3>
            @for (s of g.items; track s.key) {
              <div class="row">
                <div class="label">{{ s.label }} @if (s.description) { <small>{{ s.description }}</small> }</div>
                @switch (s.type) {
                  @case ('bool') { <nz-switch [ngModel]="draft()[s.key]" (ngModelChange)="set(s.key, $event)" [attr.aria-label]="s.label" [attr.data-testid]="'setting-' + s.key"></nz-switch> }
                  @case ('text') { <input nz-input style="width:260px" [attr.aria-label]="s.label" [ngModel]="draft()[s.key]" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key" /> }
                  @case ('string') { <input nz-input style="width:260px" [attr.aria-label]="s.label" [ngModel]="draft()[s.key]" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key" /> }
                  @default { <nz-input-number [ngModel]="draft()[s.key] ?? null" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key"></nz-input-number> }
                }
              </div>
            }
          </section>
        } @empty {
          <erp-state-panel title="No settings to change" hint="None of the enabled modules has a setting." icon="setting" />
        }
        @if (groups().length) {
          <div class="actions">
            <button *erpHasPermission="'core.settings.write'" nz-button nzType="primary" [disabled]="!dirty()" [nzLoading]="saving()" (click)="save()" data-testid="settings-save">Save settings</button>
          </div>
        }
      }
      <erp-coa-numbering-editor />
    </div>`,
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

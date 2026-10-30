import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { AdminApi, SettingView } from '../../core/api/admin.api';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { CoaNumberingEditorComponent } from './coa/coa-numbering-editor.component';

/** Workspace settings grouped by module. Every setting is declared by core or a module; this page only renders them by type. */
@Component({
  selector: 'erp-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, CoaNumberingEditorComponent, HasPermissionDirective, NzButtonModule, NzInputModule, NzInputNumberModule, NzSwitchModule],
  styles: [`
    .group-card { max-width: 740px; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px 24px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.03); }
    .group-card h3 { font-size: 16px; font-weight: 700; color: #1e293b; margin-top: 0; margin-bottom: 14px; text-transform: capitalize; border-bottom: 1px solid #f1f5f9; padding-bottom: 10px; }
    .row { display:flex; gap:16px; align-items:center; padding:12px 0; border-bottom:1px solid #f1f5f9 }
    .row:last-child { border-bottom: none; padding-bottom: 0; }
    .label { flex:1; font-weight: 500; color: #334155; font-size: 14px; }
    .label small { display:block; color: #64748b; font-weight: normal; font-size: 12.5px; margin-top: 2px; }
  `],
  template: `
    <h1 class="erp-page-title">Settings</h1>
    <div style="max-width:740px; margin-bottom:24px;">
      <erp-coa-numbering-editor />
    </div>
    @for (g of groups(); track g.module) {
      <section class="group-card">
        <h3>{{ g.module === 'core' ? 'General' : g.module }}</h3>
        @for (s of g.items; track s.key) {
          <div class="row">
            <div class="label">{{ s.label }} @if (s.description) { <small>{{ s.description }}</small> }</div>
            @switch (s.type) {
              @case ('bool') { <nz-switch [ngModel]="draft()[s.key]" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key"></nz-switch> }
              @case ('text') { <input nz-input style="width:260px" [ngModel]="draft()[s.key]" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key" /> }
              @default { <nz-input-number [ngModel]="draft()[s.key] ?? null" (ngModelChange)="set(s.key, $event)" [attr.data-testid]="'setting-' + s.key"></nz-input-number> }
            }
          </div>
        }
      </section>
    }
    <div style="max-width:740px; display:flex; justify-content:flex-end;">
      <button *erpHasPermission="'core.settings.write'" nz-button nzType="primary" nzSize="large" [disabled]="!dirty()" [nzLoading]="saving()" (click)="save()" data-testid="settings-save">Save changes</button>
    </div>`,
})
export class SettingsPage {
  private readonly api = inject(AdminApi);
  private readonly message = inject(NzMessageService);
  readonly settings = signal<SettingView[]>([]);
  readonly draft = signal<Record<string, unknown>>({});
  readonly saving = signal(false);
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
      next: () => { this.saving.set(false); this.message.success('Settings saved'); this.load(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the settings'); },
    });
  }

  private load(): void {
    this.api.settings().subscribe((list) => {
      const values = Object.fromEntries(list.map((s) => [s.key, s.value]));
      this.settings.set(list);
      this.original.set(values);
      this.draft.set({ ...values });
    });
  }
}

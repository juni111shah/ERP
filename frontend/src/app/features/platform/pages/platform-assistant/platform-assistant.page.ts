import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { Observable } from 'rxjs';
import { ProblemDetails } from '@core/api/api.models';
import { AiProviderInput, AiProviderView, AssistantBot, AssistantConsoleView, PlatformApi } from '@core/api/platform.api';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { AI_PROVIDER_PRESETS, coverageWarnings, moved, presetValues } from '../../ai-providers';
import { AssistantTryComponent } from '../../components/assistant-try/assistant-try.component';
import { PLATFORM_STYLES } from '../../platform-ui';

const COMMON_FIELDS: FieldSpec[] = [
  { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 80, hint: 'Only you see this.' },
  { key: 'baseUrl', label: 'Address', type: 'text', required: true, maxLength: 300, hint: 'The part before /chat/completions. It must start with https://.' },
  { key: 'model', label: 'Model', type: 'text', required: true, maxLength: 150, hint: "Copy the exact name from the service's model list." },
];
const USE_FIELDS: FieldSpec[] = [
  { key: 'useForBrand', label: 'Use for the public assistant', type: 'switch', default: true, hint: 'Visitor questions are sent to this service.' },
  { key: 'useForWorkspace', label: 'Clear for client data', type: 'switch', default: false, hint: "The workspace assistant sends client data to this service. Read its data terms first, and leave this off for free plans that may keep or review what you send." },
];
const ADD_FIELDS: FieldSpec[] = [
  {
    key: 'preset', label: 'Service', type: 'select', default: 'groq', options: AI_PROVIDER_PRESETS.map((p) => ({ value: p.value, label: p.label })),
    hint: 'Fills in the details below. Any service that uses the same chat format works.',
    onChange: (value) => presetValues(value),
  },
  { ...COMMON_FIELDS[0], default: presetValues('groq')!.name },
  { ...COMMON_FIELDS[1], default: presetValues('groq')!.baseUrl },
  { ...COMMON_FIELDS[2], default: presetValues('groq')!.model },
  { key: 'apiKey', label: 'API key', type: 'password', required: true, hint: 'It is stored encrypted and is never shown again.' },
  ...USE_FIELDS,
];
const EDIT_FIELDS: FieldSpec[] = [
  ...COMMON_FIELDS,
  { key: 'apiKey', label: 'New API key', type: 'password', hint: 'Leave empty to keep the current key. If you change the address, you must paste the key again.' },
  ...USE_FIELDS,
];

/** The founder's control of the AI assistants: where they appear, which services answer, and the keys to those services. */
@Component({
  selector: 'erp-platform-assistant',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzAlertModule, NzButtonModule, NzIconModule, NzInputNumberModule, NzModalModule, NzSpinModule, NzSwitchModule, AssistantTryComponent, ErpDatePipe, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .group { margin-bottom: 16px; }
    .notice { margin-bottom: 16px; }
    .show-row { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 10px 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .show-row:last-child { border-bottom: 0; padding-bottom: 0; }
    .show-row label { display: block; font-weight: 600; color: var(--erp-color-text-primary); }
    .show-row small { display: block; font-weight: 400; color: var(--erp-color-text-muted); margin-top: 2px; }
    .order { display: inline-flex; gap: 2px; }
    .row-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
    .address { max-width: 280px; overflow-wrap: anywhere; display: block; }
    .test-note { display: block; margin-top: 4px; max-width: 240px; }
    .assistant-lower { margin-top: 16px; margin-bottom: 0; }
    .limits { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; margin-top: 16px; align-items: end; }
    .limits label { display: block; font-size: 13px; color: var(--erp-color-text-muted); }
    .limits nz-input-number { display: block; width: 100%; margin-top: 4px; }
    .limits button { grid-column: 1 / -1; justify-self: start; }
    @media (max-width: 767px) { .limits { grid-template-columns: minmax(0, 1fr); } }
  `],
  templateUrl: './platform-assistant.page.html',
})
export class PlatformAssistantPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly addFields = ADD_FIELDS;
  readonly editFields = EDIT_FIELDS;
  readonly view = signal<AssistantConsoleView | null>(null);
  readonly failed = signal(false);
  readonly busy = signal(false);
  readonly testing = signal<string | null>(null);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly editing = signal<AiProviderView | null>(null);
  readonly editValue = computed<Record<string, unknown> | null>(() => {
    const p = this.editing();
    return p ? { name: p.name, baseUrl: p.baseUrl, model: p.model, useForBrand: p.useForBrand, useForWorkspace: p.useForWorkspace } : null;
  });
  readonly warnings = computed(() => { const v = this.view(); return v ? coverageWarnings(v) : []; });
  readonly brandLimit = signal<number | null>(null);
  readonly workspaceLimit = signal<number | null>(null);

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    this.api.assistant().subscribe({ next: (v) => this.show(v), error: () => this.failed.set(true) });
  }

  /** Fresh usage after a try-out. A failure here is not worth interrupting anyone for. */
  reload(): void {
    this.api.assistant().subscribe({ next: (v) => this.view.set(v), error: () => undefined });
  }

  private show(v: AssistantConsoleView): void {
    this.view.set(v);
    this.brandLimit.set(v.brandDailyTokens);
    this.workspaceLimit.set(v.workspaceDailyTokens);
  }

  /** Tokens one assistant has used today, across all its services. */
  used(bot: AssistantBot): number {
    return (this.view()?.usageToday ?? []).filter((u) => u.bot === bot).reduce((sum, u) => sum + u.inputTokens + u.outputTokens, 0);
  }

  saveLimits(): void {
    const brand = this.brandLimit();
    const workspace = this.workspaceLimit();
    if (brand === null || workspace === null) return;
    this.run(this.api.setAssistantLimits({ brandDailyTokens: brand, workspaceDailyTokens: workspace }), 'Could not save the limits', () => this.message.success('Daily token limits saved for the assistants'));
  }

  /** The switch does not move by itself: it shows what the server has saved. */
  toggle(which: 'brand' | 'workspace'): void {
    const v = this.view();
    if (!v?.canManage || this.busy()) return;
    this.run(this.api.setAssistantVisibility({
      brandEnabled: which === 'brand' ? !v.brandEnabled : v.brandEnabled,
      workspaceEnabled: which === 'workspace' ? !v.workspaceEnabled : v.workspaceEnabled,
    }), 'Could not change this setting');
  }

  move(index: number, delta: -1 | 1): void {
    const v = this.view();
    if (v) this.run(this.api.reorderAiProviders(moved(v.providers.map((p) => p.id), index, delta)), 'Could not change the order');
  }

  test(p: AiProviderView): void {
    this.testing.set(p.id);
    this.run(this.api.testAiProvider(p.id), 'Could not run the test', () => this.testing.set(null));
  }

  remove(p: AiProviderView): void {
    this.modal.confirm({
      nzTitle: `Delete ${p.name}?`,
      nzContent: 'Its key is permanently deleted from this system. The assistant moves on to the next service in the list.',
      nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.run(this.api.deleteAiProvider(p.id), 'Could not delete this service'),
    });
  }

  openAdd(): void { this.editing.set(null); this.errors.set(null); this.open.set(true); }
  openEdit(p: AiProviderView): void { this.editing.set(p); this.errors.set(null); this.open.set(true); }

  save(v: Record<string, unknown>): void {
    const input: AiProviderInput = {
      name: v['name'] as string, baseUrl: v['baseUrl'] as string, model: v['model'] as string, apiKey: (v['apiKey'] as string | null) || null,
      useForBrand: !!v['useForBrand'], useForWorkspace: !!v['useForWorkspace'],
    };
    const editing = this.editing();
    this.saving.set(true);
    this.errors.set(null);
    (editing ? this.api.updateAiProvider(editing.id, input) : this.api.createAiProvider(input)).subscribe({
      next: (view) => { this.saving.set(false); this.open.set(false); this.view.set(view); this.message.success(editing ? doneText('saved', 'AI service', input.name) : `${doneText('added', 'AI service', input.name)}. Press Test to check that it works.`); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors) this.message.error(problemMessage(e, 'Could not save this service'));
      },
    });
  }

  private run(call: Observable<AssistantConsoleView>, failure: string, done?: () => void): void {
    this.busy.set(true);
    call.subscribe({
      next: (view) => { this.busy.set(false); done?.(); this.view.set(view); },
      error: (e) => { this.busy.set(false); done?.(); this.message.error(problemMessage(e, failure)); },
    });
  }
}

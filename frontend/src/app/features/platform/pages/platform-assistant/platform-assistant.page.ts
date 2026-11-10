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
import { AiProviderView, AssistantBot, AssistantConsoleView, PlatformApi } from '@core/api/platform.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { AI_PROVIDER_ADD_FIELDS, AI_PROVIDER_EDIT_FIELDS, coverageWarnings, moved, providerDrawerValue, toAiProviderInput, tokensUsedToday } from '../../ai-providers';
import { AssistantTryComponent } from '../../components/assistant-try/assistant-try.component';
import { DrawerValue } from '../../platform-forms';
import { PLATFORM_STYLES } from '../../platform-ui';

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

  readonly addFields = AI_PROVIDER_ADD_FIELDS;
  readonly editFields = AI_PROVIDER_EDIT_FIELDS;
  readonly view = signal<AssistantConsoleView | null>(null);
  readonly failed = signal(false);
  readonly busy = signal(false);
  readonly testing = signal<string | null>(null);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly editing = signal<AiProviderView | null>(null);
  readonly editValue = computed<DrawerValue | null>(() => {
    const p = this.editing();
    return p ? providerDrawerValue(p) : null;
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
    loadInto(this.view, this.api.assistant());
  }

  private show(v: AssistantConsoleView): void {
    this.view.set(v);
    this.brandLimit.set(v.brandDailyTokens);
    this.workspaceLimit.set(v.workspaceDailyTokens);
  }

  /** Tokens one assistant has used today, across all its services. */
  used(bot: AssistantBot): number {
    return tokensUsedToday(this.view(), bot);
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

  save(v: DrawerValue): void {
    const input = toAiProviderInput(v);
    const editing = this.editing();
    this.saving.set(true);
    this.errors.set(null);
    (editing ? this.api.updateAiProvider(editing.id, input) : this.api.createAiProvider(input)).subscribe({
      next: (view) => { this.saving.set(false); this.open.set(false); this.view.set(view); this.message.success(editing ? doneText('saved', 'AI service', input.name) : `${doneText('added', 'AI service', input.name)}. Press Test to check that it works.`); },
      error: (e) => {
        this.saving.set(false);
        showDrawerSaveError(e, this.errors, this.message, 'Could not save this service');
      },
    });
  }

  /** Runs a call that answers with the whole console view, keeping `busy` up while it runs. */
  private run(call: Observable<AssistantConsoleView>, failure: string, done?: () => void): void {
    this.busy.set(true);
    call.subscribe({
      next: (view) => { this.busy.set(false); done?.(); this.view.set(view); },
      error: (e) => { this.busy.set(false); done?.(); this.message.error(problemMessage(e, failure)); },
    });
  }
}

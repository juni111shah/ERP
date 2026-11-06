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
import { ProblemDetails } from '../../core/api/api.models';
import { AiProviderInput, AiProviderView, AssistantBot, AssistantConsoleView, PlatformApi } from '../../core/api/platform.api';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { ErpDatePipe } from '../../shared/display.pipes';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { AI_PROVIDER_PRESETS, coverageWarnings, moved, presetValues } from './ai-providers';
import { AssistantTryComponent } from './assistant-try.component';
import { PLATFORM_STYLES } from './platform-ui';

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
  template: `
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the AI assistant settings" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else if (!view()) {
      <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
    } @else if (view(); as v) {
      <div class="pf-head">
        <p>Choose where the AI assistant appears and which services answer for it. To change service, add the new one and move it to the top.</p>
        @if (v.canManage) {
          <div class="pf-actions"><button nz-button nzType="primary" type="button" (click)="openAdd()" data-testid="assistant-add"><span nz-icon nzType="plus"></span>Add service</button></div>
        }
      </div>

      <nz-alert class="notice" nzType="info" nzShowIcon data-testid="assistant-not-built" nzMessage="The assistants themselves are not built yet. These settings are saved now and take effect when they are."></nz-alert>
      @if (!v.canManage) {
        <nz-alert class="notice" nzType="warning" nzShowIcon data-testid="assistant-owner-only" nzMessage="Only the platform owner can change these settings."></nz-alert>
      }
      @for (w of warnings(); track w) {
        <nz-alert class="notice" nzType="warning" nzShowIcon data-testid="assistant-coverage" [nzMessage]="w"></nz-alert>
      }

      <section class="pf-card group" data-testid="assistant-visibility">
        <h2>Where it appears</h2>
        <div class="show-row">
          <label for="assistant-brand">Public assistant<small>On your pricing and other public pages, for visitors who have not signed in.</small></label>
          <nz-switch nzId="assistant-brand" nzControl [ngModel]="v.brandEnabled" [nzDisabled]="!v.canManage || busy()" [nzLoading]="busy()" (click)="toggle('brand')" data-testid="assistant-brand"></nz-switch>
        </div>
        <div class="show-row">
          <label for="assistant-workspace">Workspace assistant<small>Inside client workspaces. It sends client data to the services you have cleared for that.</small></label>
          <nz-switch nzId="assistant-workspace" nzControl [ngModel]="v.workspaceEnabled" [nzDisabled]="!v.canManage || busy()" [nzLoading]="busy()" (click)="toggle('workspace')" data-testid="assistant-workspace"></nz-switch>
        </div>
      </section>

      <section class="pf-card" data-testid="assistant-providers">
        <h2>AI services</h2>
        @if (!v.providers.length) {
          <p class="pf-empty" data-testid="assistant-empty">No service is connected yet. Add one, then press Test to check that it works.</p>
        } @else {
          <p class="pf-muted pf-small" style="margin:-4px 0 12px">The assistant tries the top service first. If that one fails or reaches its limit, it moves to the next.</p>
          <div class="pf-scroll">
            <table class="pf-table">
              <thead><tr><th>Order</th><th>Service</th><th>Key</th><th>Used for</th><th>Last test</th><th></th></tr></thead>
              <tbody>
                @for (p of v.providers; track p.id; let i = $index) {
                  <tr [attr.data-testid]="'assistant-provider-' + p.name">
                    <td class="pf-nowrap">
                      <span class="order">
                        <button nz-button nzSize="small" nzType="text" type="button" [disabled]="!v.canManage || busy() || i === 0" (click)="move(i, -1)" [attr.aria-label]="'Move ' + p.name + ' up'" [attr.data-testid]="'assistant-up-' + p.name"><span nz-icon nzType="arrow-up"></span></button>
                        <button nz-button nzSize="small" nzType="text" type="button" [disabled]="!v.canManage || busy() || i === v.providers.length - 1" (click)="move(i, 1)" [attr.aria-label]="'Move ' + p.name + ' down'" [attr.data-testid]="'assistant-down-' + p.name"><span nz-icon nzType="arrow-down"></span></button>
                      </span>
                    </td>
                    <td><strong>{{ p.name }}</strong><span class="pf-mono address">{{ p.model }}</span><span class="pf-muted pf-small address">{{ p.baseUrl }}</span></td>
                    <td class="pf-nowrap">
                      @if (p.keyNeedsReentry) { <erp-status value="overdue" label="Paste it again" /> }
                      @else { <span class="pf-mono" [attr.data-testid]="'assistant-key-' + p.name">••••{{ p.keyHint }}</span> }
                    </td>
                    <td class="pf-small">
                      @if (p.useForBrand) { <div>Public assistant</div> }
                      @if (p.useForWorkspace) { <div>Client data</div> }
                      @if (!p.useForBrand && !p.useForWorkspace) { <span class="pf-muted">Not in use</span> }
                    </td>
                    <td>
                      @if (p.lastTestOk === null) { <span class="pf-muted pf-small">Not tested</span> }
                      @else {
                        <erp-status [value]="p.lastTestOk ? 'active' : 'overdue'" [label]="p.lastTestOk ? 'Working' : 'Failed'" />
                        <span class="pf-muted pf-small test-note" [attr.data-testid]="'assistant-test-note-' + p.name">{{ p.lastTestMessage }}@if (p.lastTestedAt) { · {{ p.lastTestedAt | erpDate }} }</span>
                      }
                    </td>
                    <td>
                      @if (v.canManage) {
                        <div class="row-actions">
                          <button nz-button nzSize="small" type="button" [nzLoading]="testing() === p.id" [disabled]="busy()" (click)="test(p)" [attr.data-testid]="'assistant-test-' + p.name">Test</button>
                          <button nz-button nzSize="small" type="button" [disabled]="busy()" (click)="openEdit(p)" [attr.data-testid]="'assistant-edit-' + p.name">Edit</button>
                          <button nz-button nzSize="small" nzDanger type="button" [disabled]="busy()" (click)="remove(p)" [attr.data-testid]="'assistant-delete-' + p.name">Delete</button>
                        </div>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <div class="pf-grid assistant-lower">
        <section class="pf-card" data-testid="assistant-usage">
          <h2>Today's usage</h2>
          <p class="pf-muted pf-small" style="margin:-4px 0 12px">Counted per day in UTC. When an assistant reaches its limit it stops until the next day, which also protects you from a bill if you use a paid key.</p>
          <dl class="pf-facts">
            <dt>Public assistant</dt><dd data-testid="assistant-used-brand">{{ used('brand') | number }} of {{ v.brandDailyTokens | number }} tokens</dd>
            <dt>Workspace assistant</dt><dd data-testid="assistant-used-workspace">{{ used('workspace') | number }} of {{ v.workspaceDailyTokens | number }} tokens</dd>
          </dl>
          @if (v.usageToday.length) {
            <div class="pf-scroll" style="margin-top:12px">
              <table class="pf-table">
                <thead><tr><th>Service</th><th>For</th><th class="num">Replies</th><th class="num">Failed</th><th class="num">Tokens</th></tr></thead>
                <tbody>
                  @for (u of v.usageToday; track u.bot + u.providerId) {
                    <tr [attr.data-testid]="'assistant-usage-' + u.providerName">
                      <td>{{ u.providerName }}@if (u.lastFailure) { <span class="pf-muted pf-small address">{{ u.lastFailure }}</span> }</td>
                      <td class="pf-nowrap">{{ u.bot === 'brand' ? 'Public' : 'Workspace' }}</td>
                      <td class="num">{{ u.requests | number }}</td>
                      <td class="num">{{ u.failures | number }}</td>
                      <td class="num">{{ u.inputTokens + u.outputTokens | number }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
          @if (v.canManage) {
            <form class="limits" (ngSubmit)="saveLimits()" data-testid="assistant-limits">
              <label>Public assistant, tokens a day
                <nz-input-number name="brandLimit" [ngModel]="brandLimit()" (ngModelChange)="brandLimit.set($event)" [nzMin]="1000" [nzMax]="100000000" [nzStep]="10000" [nzPrecision]="0" data-testid="assistant-limit-brand"></nz-input-number>
              </label>
              <label>Workspace assistant, tokens a day
                <nz-input-number name="workspaceLimit" [ngModel]="workspaceLimit()" (ngModelChange)="workspaceLimit.set($event)" [nzMin]="1000" [nzMax]="100000000" [nzStep]="10000" [nzPrecision]="0" data-testid="assistant-limit-workspace"></nz-input-number>
              </label>
              <button nz-button type="submit" [disabled]="busy()" data-testid="assistant-limits-save">Save limits</button>
            </form>
          }
        </section>

        @if (v.canManage) {
          <section class="pf-card" data-testid="assistant-try">
            <h2>Try it</h2>
            <p class="pf-muted pf-small" style="margin:-4px 0 12px">Talk to your services directly, even with the assistants switched off. Replies count towards today's usage, and nothing is saved.</p>
            <erp-assistant-try (used)="reload()" />
          </section>
        }
      </div>
    }
    <erp-record-drawer [title]="editing() ? 'Edit ' + editing()!.name : 'Add AI service'" [open]="open()" [fields]="editing() ? editFields : addFields" [value]="editValue()" [saving]="saving()" [errors]="errors()"
      [submitLabel]="editing() ? 'Save' : 'Add service'" testId="assistant-form" (submitted)="save($event)" (closed)="open.set(false)" />`,
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
    this.run(this.api.setAssistantLimits({ brandDailyTokens: brand, workspaceDailyTokens: workspace }), 'Could not save the limits', () => this.message.success('Limits saved'));
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
      next: (view) => { this.saving.set(false); this.open.set(false); this.view.set(view); this.message.success(editing ? 'Service saved' : 'Service added. Press Test to check that it works.'); },
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

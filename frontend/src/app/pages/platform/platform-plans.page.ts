import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { ProblemDetails } from '../../core/api/api.models';
import { PlanInput, PlanView, PlatformApi, PlatformModule } from '../../core/api/platform.api';
import { yearlySaving } from '../../core/format/subscription';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { ErpMoneyPipe } from '../../shared/display.pipes';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PLATFORM_STYLES } from './platform-ui';

/** The price list: what is sold, for how much, and with what limits. */
@Component({
  selector: 'erp-platform-plans',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule, NzModalModule, NzSkeletonModule, ErpMoneyPipe, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .cards { display: grid; gap: 16px; grid-template-columns: repeat(auto-fill, minmax(290px, 1fr)); }
    .plan { display: flex; flex-direction: column; }
    .plan.archived { opacity: .72; }
    .title { display: flex; justify-content: space-between; gap: 8px; align-items: flex-start; }
    .title h2 { margin: 0; font-size: 16px; font-weight: 700; overflow-wrap: anywhere; color: var(--erp-color-text-primary); }
    .tags { display: flex; flex-wrap: wrap; gap: 4px; margin: 6px 0 10px; }
    .desc { margin: 0 0 10px; color: var(--erp-color-text-muted); font-size: 13px; line-height: 1.45; }
    .price { font-size: 20px; font-weight: 800; color: var(--erp-color-text-primary); letter-spacing: -.01em; }
    .price small { font-size: 13px; font-weight: 500; color: var(--erp-color-text-muted); }
    .year { color: var(--erp-color-text-secondary); font-size: 13px; margin-bottom: 12px; }
    .year em { font-style: normal; color: var(--erp-color-success-text); font-weight: 600; }
    .pf-facts { margin-bottom: 14px; font-size: 13px; flex: 1; align-content: start; }
    .row { display: flex; gap: 8px; flex-wrap: wrap; }
  `],
  template: `
    <div class="pf-head">
      <p>Public plans appear on the registration page. Private plans are only for clients you assign by hand.</p>
      <div class="pf-actions"><button nz-button nzType="primary" type="button" (click)="edit(null)" data-testid="plans-new"><span nz-icon nzType="plus"></span>New plan</button></div>
    </div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the plans" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else if (loading()) {
      <div class="cards">@for (i of [1, 2, 3]; track i) { <div class="pf-card"><nz-skeleton [nzActive]="true"></nz-skeleton></div> }</div>
    } @else if (plans().length === 0) {
      <erp-state-panel icon="tag" title="No plans yet" hint="Create the first plan to start taking registrations." actionLabel="New plan" (action)="edit(null)" />
    } @else {
      <div class="cards">
        @for (p of plans(); track p.id) {
          <section class="pf-card plan" [class.archived]="!p.isActive" [attr.data-testid]="'plan-' + p.code">
            <div class="title"><h2>{{ p.name }}</h2><span class="pf-mono pf-muted">{{ p.code }}</span></div>
            <div class="tags">
              @if (!p.isActive) { <erp-status value="archived" label="Archived" /> }
              <erp-status [value]="p.isPublic ? 'published' : 'inactive'" [label]="p.isPublic ? 'Public' : 'Private'" />
              @if (p.isRecommended) { <erp-status value="new" label="Recommended" /> }
            </div>
            @if (p.description) { <p class="desc">{{ p.description }}</p> }
            <div class="price">{{ p.monthlyPrice | erpMoney: p.currency }} <small>a month</small></div>
            <div class="year">{{ p.annualPrice | erpMoney: p.currency }} a year @if (yearly(p); as s) { <em>saves {{ s.percent }}%</em> }</div>
            <dl class="pf-facts">
              <dt>Trial</dt><dd>{{ p.trialDays > 0 ? p.trialDays + ' days' : 'None' }}</dd>
              <dt>Users</dt><dd>{{ p.maxUsers ?? 'No limit' }}</dd>
              <dt>Legal entities</dt><dd>{{ p.maxLegalEntities ?? 'No limit' }}</dd>
              <dt>Assistant messages a month</dt><dd>{{ p.assistantMonthlyMessages === 0 ? 'Not included' : p.assistantMonthlyMessages ?? 'No limit' }}</dd>
              <dt>Modules</dt><dd>{{ moduleText(p) }}</dd>
              <dt>Clients</dt><dd data-testid="plan-client-count">{{ p.clientCount }}</dd>
            </dl>
            <div class="row">
              <button nz-button nzSize="small" type="button" (click)="edit(p)" [attr.data-testid]="'plan-edit-' + p.code"><span nz-icon nzType="edit"></span>Edit</button>
              <button nz-button nzSize="small" type="button" [nzDanger]="p.isActive" (click)="toggleArchive(p)" [attr.data-testid]="'plan-archive-' + p.code">{{ p.isActive ? 'Archive' : 'Restore' }}</button>
            </div>
          </section>
        }
      </div>
    }
    <erp-record-drawer [title]="editing() ? 'Edit ' + editing()!.name : 'New plan'" [open]="open()" [fields]="fields()" [value]="value()" [saving]="saving()" [errors]="errors()" [width]="720" testId="plan-form"
                       (submitted)="save($event)" (closed)="open.set(false)" />`,
})
export class PlatformPlansPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly plans = signal<PlanView[]>([]);
  readonly modules = signal<PlatformModule[]>([]);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly editing = signal<PlanView | null>(null);
  readonly value = computed<Record<string, unknown> | null>(() => {
    const p = this.editing();
    return p ? { ...p, features: p.features.join('\n') } : null;
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 39, hint: 'Lower-case letters, digits or dashes. Used in links such as /register?plan=starter.' },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 120 },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 500 },
    { key: 'monthlyPrice', label: 'Monthly price', type: 'number', required: true, min: 0, default: 0 },
    { key: 'annualPrice', label: 'Annual price', type: 'number', required: true, min: 0, default: 0, hint: 'For the whole year, not per month.' },
    { key: 'trialDays', label: 'Trial days', type: 'number', required: true, min: 0, max: 365, precision: 0, default: 14 },
    { key: 'sortOrder', label: 'Sort order', type: 'number', required: true, precision: 0, default: 100, hint: 'Lower numbers come first on the price list.' },
    { key: 'maxUsers', label: 'Maximum users', type: 'number', min: 1, precision: 0, hint: 'Leave empty for no limit.' },
    { key: 'maxLegalEntities', label: 'Maximum legal entities', type: 'number', min: 1, precision: 0, hint: 'Leave empty for no limit.' },
    { key: 'assistantMonthlyMessages', label: 'AI assistant messages per month', type: 'number', min: 0, precision: 0, hint: 'For the whole workspace. Leave empty for no limit, or use 0 to leave the assistant out of this plan.' },
    { key: 'modules', label: 'Modules', type: 'multiselect', options: this.modules().map((m) => ({ value: m.key, label: m.name })), hint: 'Leave empty to include every module.' },
    { key: 'features', label: 'Features', type: 'textarea', maxLength: 2000, hint: 'One per line. These are the selling points on the pricing card.' },
    { key: 'isPublic', label: 'Public', type: 'switch', default: true, hint: 'Shown on the registration page.' },
    { key: 'isRecommended', label: 'Recommended', type: 'switch', hint: 'Highlighted on the pricing cards.' },
  ]);

  constructor() {
    this.load();
    this.api.modules().subscribe({ next: (m) => this.modules.set(m.filter((x) => !x.isCore)), error: () => undefined });
  }

  load(): void {
    this.failed.set(false);
    this.api.plans().subscribe({
      next: (p) => { this.plans.set(p); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  yearly(p: PlanView): { amount: number; percent: number } | null { return yearlySaving(p.monthlyPrice, p.annualPrice); }

  moduleText(p: PlanView): string {
    if (!p.modules.length) return 'Every module';
    const names = new Map(this.modules().map((m) => [m.key, m.name]));
    return p.modules.map((k) => names.get(k) ?? k).join(', ');
  }

  edit(plan: PlanView | null): void {
    this.errors.set(null);
    this.editing.set(plan);
    this.open.set(true);
  }

  save(v: Record<string, unknown>): void {
    const input: PlanInput = {
      code: v['code'] as string, name: v['name'] as string, description: v['description'] as string | null, isPublic: !!v['isPublic'], isRecommended: !!v['isRecommended'], sortOrder: (v['sortOrder'] as number) ?? 0,
      monthlyPrice: (v['monthlyPrice'] as number) ?? 0, annualPrice: (v['annualPrice'] as number) ?? 0, trialDays: (v['trialDays'] as number) ?? 0, maxUsers: (v['maxUsers'] as number | null) ?? null, maxLegalEntities: (v['maxLegalEntities'] as number | null) ?? null, assistantMonthlyMessages: (v['assistantMonthlyMessages'] as number | null) ?? null,
      modules: (v['modules'] as string[] | null) ?? [], features: String(v['features'] ?? '').split('\n').map((f) => f.trim()).filter(Boolean),
    };
    const editing = this.editing();
    this.saving.set(true);
    this.errors.set(null);
    (editing ? this.api.updatePlan(editing.id, input) : this.api.createPlan(input)).subscribe({
      next: (saved) => { this.saving.set(false); this.open.set(false); this.message.success(`${saved.name} saved`); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors) this.message.error(problemMessage(e, 'Could not save the plan'));
      },
    });
  }

  toggleArchive(p: PlanView): void {
    const archive = p.isActive;
    this.modal.confirm({
      nzTitle: archive ? `Archive ${p.name}?` : `Restore ${p.name}?`,
      nzContent: archive ? 'It can no longer be chosen. Clients already on it stay on it.' : 'It can be chosen again, and appears on the registration page if it is public.',
      nzOkText: archive ? 'Archive' : 'Restore',
      nzOkDanger: archive,
      nzCancelText: 'Not now',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.setPlanArchived(p.id, archive).subscribe({
          next: () => { this.message.success(archive ? `${p.name} archived` : `${p.name} restored`); this.load(); resolve(); },
          error: (e) => { this.message.error(problemMessage(e, 'Could not change the plan')); resolve(); },
        });
      }),
    });
  }
}

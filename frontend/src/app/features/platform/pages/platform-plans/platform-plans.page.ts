import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { ProblemDetails } from '@core/api/api.models';
import { PlanView, PlatformApi, PlatformModule } from '@core/api/platform.api';
import { yearlySaving } from '@core/format/subscription';
import { problemMessage } from '@core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { DrawerValue, planDrawerValue, planFields, planModuleText, toPlanInput } from '../../platform-forms';
import { PLATFORM_STYLES } from '../../platform-ui';

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
  templateUrl: './platform-plans.page.html',
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
  readonly value = computed<DrawerValue | null>(() => {
    const p = this.editing();
    return p ? planDrawerValue(p) : null;
  });
  readonly fields = computed<FieldSpec[]>(() => planFields(this.modules()));

  constructor() {
    this.load();
    loadInto(this.modules, this.api.modules(), (m) => m.filter((x) => !x.isCore));
  }

  load(): void {
    this.failed.set(false);
    this.api.plans().subscribe({
      next: (p) => { this.plans.set(p); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  yearly(p: PlanView): { amount: number; percent: number } | null { return yearlySaving(p.monthlyPrice, p.annualPrice); }

  moduleText(p: PlanView): string { return planModuleText(p, this.modules()); }

  edit(plan: PlanView | null): void {
    this.errors.set(null);
    this.editing.set(plan);
    this.open.set(true);
  }

  save(v: DrawerValue): void {
    const input = toPlanInput(v);
    const editing = this.editing();
    this.saving.set(true);
    this.errors.set(null);
    (editing ? this.api.updatePlan(editing.id, input) : this.api.createPlan(input)).subscribe({
      next: (saved) => { this.saving.set(false); this.open.set(false); this.message.success(`${saved.name} saved`); this.load(); },
      error: (e) => {
        this.saving.set(false);
        showDrawerSaveError(e, this.errors, this.message, 'Could not save the plan');
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

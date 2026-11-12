import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { AcceptanceDto, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { MilestoneDto, MilestoneInput, ProjectDto, ProjectMilestonesApi } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { MasterDataState } from '@shared/utils/master-data-state';
import { isoToday } from '../../projects-format';
import { withIsoDates } from '../../projects-mappers';
import { errorText, PRJ_STYLES, PRJ_SUBTABS } from '../../projects-ui';
import { milestonesSummary } from '../../projects-view';
import { ProjectTagComponent } from '../project-tag/project-tag.component';

/** Milestones with everything that can happen to one, and the customer's sign-offs. */
@Component({
  selector: 'erp-project-milestones',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DatePipe, DecimalPipe, NzButtonModule, NzIconModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  templateUrl: './project-milestones.component.html',
  styles: [PRJ_STYLES + PRJ_SUBTABS + '.signoffs { margin-top:12px; } .acts { white-space:nowrap; } .acts button { margin-inline-start:4px; } .evidence { max-width:260px; font-size:11.5px; }'],
})
export class ProjectMilestonesComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly api = inject(ProjectMilestonesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly todayIso = isoToday();
  readonly rows = signal<MilestoneDto[]>([]);
  readonly acceptances = signal<AcceptanceDto[]>([]);
  readonly hasCustomer = computed(() => !!this.project().customerId);
  readonly summary = computed(() => milestonesSummary(this.rows(), this.project(), this.currency()));

  readonly state: MasterDataState<MilestoneDto> = new MasterDataState<MilestoneDto>({
    save: (id, body) => {
      const editing = this.state.editing();
      const fields = { ...(body as Record<string, unknown>), projectId: this.project().id, status: editing?.status ?? 'pending', achievedDate: editing?.achievedDate ?? null };
      const input = withIsoDates(fields, 'dueDate') as unknown as MilestoneInput;
      return id ? this.api.update(id, input) : this.api.create(input);
    },
    reload: () => this.refresh(), message: inject(NzMessageService), entityName: 'Milestone',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'title', label: 'Milestone', type: 'text', required: true, maxLength: 200 },
    { key: 'dueDate', label: 'Target date', type: 'date', required: true },
    { key: 'amount', label: `Amount to bill (${this.currency()})`, type: 'number', required: true, min: 0, default: 0, precision: 2 },
  ]);

  constructor() { effect(() => { this.project().id; this.load(); }); }

  load(): void {
    const id = this.project().id;
    this.api.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'projectId', op: 'eq', value: id }] }).subscribe({
      next: (r) => this.rows.set(r.items),
      error: (e) => this.msg.error(errorText(e, 'Could not load the milestones.')),
    });
    loadOrFallback(this.acceptances, this.extra.acceptances(id), []);
  }

  signOf(milestoneId: string): AcceptanceDto | undefined { return this.acceptances().find((a) => a.milestoneId === milestoneId); }
  waiting(milestoneId: string): boolean { return this.acceptances().some((a) => a.milestoneId === milestoneId && a.status === 'pending'); }

  achieve(m: MilestoneDto): void {
    this.api.achieve(m.id).subscribe({
      next: () => { this.msg.success(`Milestone ${m.title} achieved. It is now ready to bill.`); this.refresh(); },
      error: (e) => this.msg.error(errorText(e, 'Could not mark it achieved.')),
    });
  }

  setStatus(m: MilestoneDto, status: MilestoneDto['status']): void {
    const input: MilestoneInput = { projectId: m.projectId, title: m.title, dueDate: m.dueDate, amount: m.amount, status, achievedDate: status === 'achieved' ? m.achievedDate : null };
    this.api.update(m.id, input).subscribe({ next: () => this.refresh(), error: (e) => this.msg.error(errorText(e, 'Could not change the milestone.')) });
  }

  remove(m: MilestoneDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this milestone?', nzContent: m.title, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(m.id).subscribe({ next: () => this.refresh(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }),
    });
  }

  askSignOff(m: MilestoneDto): void { this.request(m.id, `Sign-off of ${m.title}`); }
  askProjectSignOff(): void { this.request(null, `Sign-off of ${this.project().name}`); }

  /** Reloads the milestones and tells the project page its totals changed. */
  private refresh(): void { this.load(); this.changed.emit(); }

  private request(milestoneId: string | null, title: string): void {
    this.extra.requestAcceptance({ projectId: this.project().id, milestoneId, title }).subscribe({
      next: () => { this.msg.success(`The customer can sign ${title} in their portal`); this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not ask for the sign-off.')),
    });
  }
}

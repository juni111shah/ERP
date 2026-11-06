import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import {
  EngagementSummaryDto,
  RetainerCycle,
  RetainerStatus,
  RetainerSummaryDto,
  ServicesEngagementsApi,
  ServicesRetainersApi,
} from '../../core/api/services.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { ErpFormModule } from '../../core/forms/erp-form.module';

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

const CYCLE_PERIOD: Record<RetainerCycle, string> = { monthly: 'month', quarterly: 'quarter', annual: 'year' };

@Component({
  selector: 'erp-retainers-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpLabelPipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzIconModule,
    NzDrawerModule,
    ErpFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzPopconfirmModule,
    NzSelectModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  template: `
    <!-- The server allows no search or sorting on retainers yet, so neither is offered. -->
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, title or client" [clickable]="false" emptyText="No retainers yet. Add one to bill a client on a regular cycle.">
      <button toolbar nz-button nzType="primary" *erpHasPermission="'services.retainers.manage'" (click)="openCreateDrawer()" data-testid="retainer-new">
        <span nz-icon nzType="plus"></span> New Retainer
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('retainerNumber') { <span class="code strong">{{ row.retainerNumber }}</span> }
          @case ('title') {
            <div>{{ row.title }}</div>
            <div class="sub">{{ row.clientName }}</div>
          }
          @case ('cycle') { {{ row.cycle | erpLabel }} }
          @case ('recurringFee') {
            <span class="num strong">{{ currency() }} {{ row.recurringFee | number:'1.2-2' }}</span>
            <span class="sub nowrap"> / {{ period(row.cycle) }}</span>
          }
          @case ('includedHours') { <span class="num">{{ row.includedHours | number:'1.0-2' }} h</span> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('actions') {
            <div class="table-actions" *erpHasPermission="'services.retainers.manage'">
              @if (row.status === 'active') {
                <button nz-button nzType="link" nzSize="small" (click)="setStatus(row, 'paused')">
                  <span nz-icon nzType="pause-circle"></span> Pause
                </button>
              }
              @if (row.status === 'paused') {
                <button nz-button nzType="link" nzSize="small" (click)="setStatus(row, 'active')">
                  <span nz-icon nzType="play-circle"></span> Resume
                </button>
              }
              @if (row.status !== 'terminated') {
                <button nz-button nzType="link" nzDanger nzSize="small"
                        nz-popconfirm [nzPopconfirmTitle]="'Terminate ' + row.retainerNumber + '? This cannot be undone.'" (nzOnConfirm)="setStatus(row, 'terminated')">
                  <span nz-icon nzType="stop"></span> Terminate
                </button>
              }
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <!-- Create Retainer Drawer -->
    <nz-drawer [nzVisible]="drawerVisible()" nzTitle="New retainer" [nzWidth]="500" (nzOnClose)="drawerVisible.set(false)">
      <ng-container *nzDrawerContent>
        <form nz-form [formGroup]="form" (ngSubmit)="submitRetainer()" nzLayout="vertical">
          <nz-form-item>
            <nz-form-label nzRequired>Engagement</nz-form-label>
            <nz-form-control nzErrorTip="Choose an engagement" [nzExtra]="selectedEngagement() ? 'Client: ' + selectedEngagement()!.clientName : undefined">
              <nz-select formControlName="engagementId" nzShowSearch nzPlaceHolder="Choose an engagement" nzNotFoundContent="No open engagements. Create one first.">
                @for (eng of openEngagements(); track eng.id) {
                  <nz-option [nzValue]="eng.id" [nzLabel]="eng.engagementCode + ' - ' + eng.title"></nz-option>
                }
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Title</nz-form-label>
            <nz-form-control nzErrorTip="Enter a title">
              <input nz-input formControlName="title" placeholder="e.g. Monthly advisory retainer" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Billing cycle</nz-form-label>
            <nz-form-control>
              <nz-select formControlName="cycle">
                <nz-option nzValue="monthly" nzLabel="Monthly"></nz-option>
                <nz-option nzValue="quarterly" nzLabel="Quarterly"></nz-option>
                <nz-option nzValue="annual" nzLabel="Annual"></nz-option>
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <div class="row-3">
            <nz-form-item>
              <nz-form-label nzRequired>Fee ({{ currency() }})</nz-form-label>
              <nz-form-control nzErrorTip="Enter a fee">
                <nz-input-number formControlName="recurringFee" [nzMin]="0" [nzStep]="500" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Hours included</nz-form-label>
              <nz-form-control nzErrorTip="Enter the hours">
                <nz-input-number formControlName="includedHours" [nzMin]="0" [nzStep]="5" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Extra hour rate</nz-form-label>
              <nz-form-control nzErrorTip="Enter a rate">
                <nz-input-number formControlName="overageHourlyRate" [nzMin]="0" [nzStep]="25" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
          </div>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Start date</nz-form-label>
              <nz-form-control nzErrorTip="Choose a start date">
                <nz-date-picker formControlName="startDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>End date</nz-form-label>
              <nz-form-control nzErrorTip="The end date cannot be before the start date">
                <nz-date-picker formControlName="endDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
          </div>

          <nz-form-item>
            <nz-form-label>Terms</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="terms" rows="3" placeholder="Turnaround, what happens to unused hours"></textarea>
            </nz-form-control>
          </nz-form-item>

          <div class="drawer-footer">
            <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
            <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit">Save</button>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
  `,
  styles: [`
    .code { font-family: var(--erp-font-family-mono); white-space: nowrap; }
    .strong { font-weight: 600; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .row-2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .row-3 { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; }
    .full { width: 100%; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-block-start: 24px; }
  `],
})
export class RetainersPage implements OnInit {
  private readonly api = inject(ServicesRetainersApi);
  private readonly engagementsApi = inject(ServicesEngagementsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList<RetainerSummaryDto>((q) => this.api.list(q));
  private readonly engagements = signal<EngagementSummaryDto[]>([]);
  readonly openEngagements = computed(() => this.engagements().filter((e) => e.status === 'draft' || e.status === 'active' || e.status === 'onHold'));
  private readonly engagementId = signal<string | null>(null);
  readonly selectedEngagement = computed(() => this.engagements().find((e) => e.id === this.engagementId()) ?? null);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly columns: TableColumn<RetainerSummaryDto>[] = [
    { key: 'retainerNumber', label: 'Number', sortField: 'retainerNumber' },
    { key: 'title', label: 'Retainer and client', sortField: 'title' },
    { key: 'cycle', label: 'Cycle', sortField: 'cycle' },
    { key: 'recurringFee', label: 'Fee', sortField: 'recurringFee', align: 'right' },
    { key: 'includedHours', label: 'Hours included', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['active', 'paused', 'terminated'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly form: FormGroup = this.fb.group({
    engagementId: [null, [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    cycle: ['monthly' as RetainerCycle, [Validators.required]],
    recurringFee: [null, [Validators.required, Validators.min(0)]],
    includedHours: [null, [Validators.required, Validators.min(0)]],
    overageHourlyRate: [null, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    endDate: [null, [notBeforeStart('startDate')]],
    terms: [''],
  });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('endDate')!.updateValueAndValidity());
    this.engagementsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.engagements.set(res.items),
    });
    this.form.get('engagementId')!.valueChanges.subscribe((id) => this.engagementId.set(id));
  }

  period(cycle: RetainerCycle): string {
    return CYCLE_PERIOD[cycle];
  }

  openCreateDrawer(): void {
    this.form.reset({
      engagementId: null,
      title: '',
      cycle: 'monthly',
      recurringFee: null,
      includedHours: null,
      overageHourlyRate: null,
      startDate: new Date(),
      endDate: null,
      terms: '',
    });
    this.drawerVisible.set(true);
  }

  setStatus(row: RetainerSummaryDto, status: RetainerStatus): void {
    this.api.updateStatus(row.id, status).subscribe({
      next: () => {
        const done = status === 'paused' ? 'paused' : status === 'active' ? 'resumed' : 'terminated';
        this.msg.success(`${row.retainerNumber} ${done}`);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The retainer could not be updated')),
    });
  }

  submitRetainer(): void {
    const engagement = this.selectedEngagement();
    if (this.form.invalid || !engagement) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    this.api.create({
      legalEntityId: engagement.legalEntityId,
      engagementId: engagement.id,
      clientId: engagement.clientId,
      clientName: engagement.clientName,
      title: val.title,
      cycle: val.cycle,
      recurringFee: val.recurringFee,
      includedHours: val.includedHours,
      overageHourlyRate: val.overageHourlyRate,
      startDate: toIsoDate(new Date(val.startDate)),
      endDate: val.endDate ? toIsoDate(new Date(val.endDate)) : null,
      terms: val.terms || null,
    }).subscribe({
      next: () => {
        this.msg.success('Retainer saved');
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The retainer could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }
}

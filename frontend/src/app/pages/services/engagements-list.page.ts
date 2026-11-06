import { doneText } from '../../core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import {
  EngagementDto,
  EngagementStatus,
  EngagementSummaryDto,
  EngagementType,
  ServicesEngagementsApi,
} from '../../core/api/services.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { ErpDatePipe, ErpLabelPipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PartiesApi, PartyDto } from '../parties.page';

/** The moves the server allows from each status. */
const NEXT_STATUS: Record<EngagementStatus, EngagementStatus[]> = {
  draft: ['active', 'cancelled'],
  active: ['onHold', 'completed', 'cancelled'],
  onHold: ['active', 'cancelled'],
  completed: [],
  cancelled: [],
};

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

@Component({
  selector: 'erp-engagements-list-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpLabelPipe,
    ErpDatePipe,
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzProgressModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
    StatusTagComponent,
  ],
  template: `
    <!-- The server allows no search or sorting on engagements yet, so neither is offered. -->
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, title or client" (rowClick)="viewDetails($event)" emptyText="No engagements yet. Create one to start tracking client work.">
      <button toolbar *erpHasPermission="'services.engagements.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="engagement-new">
        <span nz-icon nzType="plus"></span> New Engagement
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('engagementCode') { <span class="code strong">{{ row.engagementCode }}</span> }
          @case ('title') {
            <div>{{ row.title }}</div>
            <div class="sub">{{ row.clientName }}</div>
          }
          @case ('type') { {{ row.type | erpLabel }} }
          @case ('hours') {
            <div class="hours">
              <div class="sub nowrap">{{ row.consumedHours | number:'1.0-1' }} of {{ row.budgetedHours | number:'1.0-1' }} h</div>
              <nz-progress
                [nzPercent]="getHoursPercent(row)"
                [nzStatus]="row.consumedHours > row.budgetedHours && row.budgetedHours > 0 ? 'exception' : 'normal'"
                [nzShowInfo]="false"
                [nzStrokeWidth]="6">
              </nz-progress>
            </div>
          }
          @case ('contractValue') { <span class="num strong">{{ currency() }} {{ row.contractValue | number:'1.2-2' }}</span> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('actions') {
            <div class="table-actions" (click)="$event.stopPropagation()">
              @if (nextStatuses(row.status).length > 0) {
                <button *erpHasPermission="'services.engagements.manage'" nz-button nzType="link" nzSize="small" (click)="openStatusModal(row)">
                  <span nz-icon nzType="edit"></span> Change status
                </button>
              }
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <!-- Create Engagement Drawer -->
    <nz-drawer [nzVisible]="drawerVisible()" nzTitle="New engagement" [nzWidth]="520" (nzOnClose)="drawerVisible.set(false)">
      <ng-container *nzDrawerContent>
        <form nz-form [formGroup]="form" (ngSubmit)="submitEngagement()" nzLayout="vertical">
          <nz-form-item>
            <nz-form-label nzRequired>Legal entity</nz-form-label>
            <nz-form-control nzErrorTip="Choose a legal entity">
              <nz-select formControlName="legalEntityId" nzPlaceHolder="Choose a legal entity">
                @for (entity of legalEntities(); track entity.id) {
                  <nz-option [nzValue]="entity.id" [nzLabel]="entity.name"></nz-option>
                }
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Title</nz-form-label>
            <nz-form-control nzErrorTip="Enter a title">
              <input nz-input formControlName="title" placeholder="e.g. Cloud migration" />
            </nz-form-control>
          </nz-form-item>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Client</nz-form-label>
              <nz-form-control nzErrorTip="Choose a client">
                <nz-select formControlName="clientId" nzShowSearch nzPlaceHolder="Choose a client" (ngModelChange)="onClientSelected($event)">
                  @for (c of clients(); track c.id) {
                    <nz-option [nzValue]="c.id" [nzLabel]="c.name + ' (' + c.code + ')'"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Billing model</nz-form-label>
              <nz-form-control>
                <nz-select formControlName="type">
                  <nz-option nzValue="fixedPrice" nzLabel="Fixed price"></nz-option>
                  <nz-option nzValue="timeAndMaterials" nzLabel="Time and materials"></nz-option>
                  <nz-option nzValue="retainer" nzLabel="Retainer"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>
          </div>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Contract value ({{ currency() }})</nz-form-label>
              <nz-form-control nzErrorTip="Enter a value">
                <nz-input-number formControlName="contractValue" [nzMin]="0" [nzStep]="1000" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Budgeted hours</nz-form-label>
              <nz-form-control nzErrorTip="Enter the hours">
                <nz-input-number formControlName="budgetedHours" [nzMin]="0" [nzStep]="10" class="full"></nz-input-number>
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
              <nz-form-label>Target end date</nz-form-label>
              <nz-form-control nzErrorTip="The end date cannot be before the start date">
                <nz-date-picker formControlName="targetEndDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
          </div>

          <nz-form-item>
            <nz-form-label>Lead consultant</nz-form-label>
            <nz-form-control>
              <input nz-input formControlName="leadConsultantName" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label>Scope</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="description" rows="3" placeholder="Objectives and deliverables"></textarea>
            </nz-form-control>
          </nz-form-item>

          <div class="drawer-footer">
            <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
            <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit" [disabled]="form.invalid">Save</button>
          </div>
        </form>
      </ng-container>
    </nz-drawer>

    <!-- Status Modal -->
    <nz-modal
      [nzVisible]="statusModalVisible()"
      [nzTitle]="'Change status of ' + (selectedEngagement()?.engagementCode ?? '')"
      nzOkText="Save"
      [nzOkDisabled]="!newStatus"
      [nzOkLoading]="isSavingStatus()"
      (nzOnCancel)="statusModalVisible.set(false)"
      (nzOnOk)="saveStatus()">
      <ng-container *nzModalContent>
        <p>Now {{ (selectedEngagement()?.status ?? '') | erpLabel }}. Move it to:</p>
        <nz-select [(ngModel)]="newStatus" class="full" nzPlaceHolder="Choose a status" aria-label="New status">
          @for (s of statusOptions(); track s) {
            <nz-option [nzValue]="s" [nzLabel]="s | erpLabel"></nz-option>
          }
        </nz-select>
      </ng-container>
    </nz-modal>

    <!-- Details Drawer -->
    <nz-drawer [nzVisible]="detailsVisible()" [nzTitle]="selectedDetails()?.engagementCode ?? 'Engagement'" [nzWidth]="480" (nzOnClose)="detailsVisible.set(false)">
      <ng-container *nzDrawerContent>
        @if (selectedDetails(); as eng) {
          <div class="detail">
            <div class="detail-tags">
              <erp-status [value]="eng.status" />
              <span class="sub">{{ eng.type | erpLabel }}</span>
            </div>
            <h3 class="detail-title">{{ eng.title }}</h3>
            <div class="sub">{{ eng.clientName }}</div>
            @if (eng.description) { <p>{{ eng.description }}</p> }

            <nz-card nzSize="small" nzTitle="Money and hours">
              <dl class="facts">
                <div><dt>Contract value</dt><dd class="num">{{ currency() }} {{ eng.contractValue | number:'1.2-2' }}</dd></div>
                <div><dt>Cost so far</dt><dd class="num">{{ currency() }} {{ eng.consumedCost | number:'1.2-2' }}</dd></div>
                <div><dt>Budgeted hours</dt><dd class="num">{{ eng.budgetedHours | number:'1.0-1' }} h</dd></div>
                <div><dt>Hours used</dt><dd class="num">{{ eng.consumedHours | number:'1.0-1' }} h</dd></div>
              </dl>
            </nz-card>

            <nz-card nzSize="small" nzTitle="Schedule">
              <dl class="facts">
                <div><dt>Start date</dt><dd>{{ eng.startDate | erpDate }}</dd></div>
                <div><dt>Target end date</dt><dd>{{ eng.targetEndDate ? (eng.targetEndDate | erpDate) : 'Not set' }}</dd></div>
                @if (eng.actualEndDate) { <div><dt>Ended on</dt><dd>{{ eng.actualEndDate | erpDate }}</dd></div> }
                <div><dt>Lead consultant</dt><dd>{{ eng.leadConsultantName || 'Not assigned' }}</dd></div>
              </dl>
            </nz-card>
          </div>
        }
      </ng-container>
    </nz-drawer>
  `,
  styles: [`
    .code { font-family: var(--erp-font-family-mono); white-space: nowrap; }
    .strong { font-weight: 600; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .hours { min-width: 130px; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .row-2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .full { width: 100%; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-block-start: 24px; }
    .detail { display: flex; flex-direction: column; gap: 12px; }
    .detail-tags { display: flex; align-items: center; gap: 8px; }
    .detail-title { margin: 0; font-size: 16px; font-weight: 600; }
    .facts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px 16px; margin: 0; }
    .facts dt { font-size: 12px; color: var(--erp-color-text-muted); }
    .facts dd { margin: 2px 0 0; }
  `],
})
export class EngagementsListPage implements OnInit {
  private readonly api = inject(ServicesEngagementsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList<EngagementSummaryDto>((q) => this.api.list(q));

  readonly columns: TableColumn<EngagementSummaryDto>[] = [
    { key: 'engagementCode', label: 'Number', sortField: 'engagementCode' },
    { key: 'title', label: 'Engagement and client', sortField: 'title' },
    { key: 'type', label: 'Billing model', sortField: 'type' },
    { key: 'hours', label: 'Hours used', filter: false, value: (r) => `${r.consumedHours} of ${r.budgetedHours}` },
    { key: 'contractValue', label: 'Contract value', sortField: 'contractValue', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['draft', 'active', 'onHold', 'completed', 'cancelled'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly clients = signal<PartyDto[]>([]);
  readonly drawerVisible = signal(false);
  readonly detailsVisible = signal(false);
  readonly statusModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isSavingStatus = signal(false);
  readonly selectedEngagement = signal<EngagementSummaryDto | null>(null);
  readonly selectedDetails = signal<EngagementDto | null>(null);
  readonly statusOptions = computed(() => this.nextStatuses(this.selectedEngagement()?.status));

  newStatus: EngagementStatus | null = null;

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    clientId: [null, [Validators.required]],
    clientName: ['', [Validators.required]],
    type: ['fixedPrice' as EngagementType, [Validators.required]],
    contractValue: [null, [Validators.required, Validators.min(0)]],
    budgetedHours: [null, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    targetEndDate: [null, [notBeforeStart('startDate')]],
    leadConsultantName: [''],
    description: [''],
  });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('targetEndDate')!.updateValueAndValidity());
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.legalEntities.set(res.items.map((e) => ({ id: e.id, name: e.name }))),
    });
    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        const customers = res.items.filter((p) => !p.roles?.length || p.roles.some((r) => /customer|client/i.test(r)));
        this.clients.set(customers.length > 0 ? customers : res.items);
      },
    });
  }

  nextStatuses(status: EngagementStatus | undefined): EngagementStatus[] {
    return status ? NEXT_STATUS[status] : [];
  }

  onClientSelected(id: string): void {
    const c = this.clients().find((x) => x.id === id);
    if (c) this.form.patchValue({ clientName: c.name });
  }

  openCreateDrawer(): void {
    const entity = this.legalEntities().find((e) => e.id === this.session.currentEntityId()) ?? this.legalEntities()[0];
    this.form.reset({
      legalEntityId: entity?.id ?? '',
      title: '',
      clientId: null,
      clientName: '',
      type: 'fixedPrice',
      contractValue: null,
      budgetedHours: null,
      startDate: new Date(),
      targetEndDate: null,
      leadConsultantName: '',
      description: '',
    });
    this.drawerVisible.set(true);
  }

  getHoursPercent(row: EngagementSummaryDto): number {
    if (!row.budgetedHours) return 0;
    return Math.min(Math.round((row.consumedHours / row.budgetedHours) * 100), 100);
  }

  openStatusModal(row: EngagementSummaryDto): void {
    this.selectedEngagement.set(row);
    this.newStatus = null;
    this.statusModalVisible.set(true);
  }

  saveStatus(): void {
    const item = this.selectedEngagement();
    if (!item || !this.newStatus) return;

    this.isSavingStatus.set(true);
    this.api.updateStatus(item.id, this.newStatus).subscribe({
      next: () => {
        this.msg.success(`${item.engagementCode} is now ${String(this.newStatus).replace(/_/g, ' ')}`);
        this.isSavingStatus.set(false);
        this.statusModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The status could not be changed'));
        this.isSavingStatus.set(false);
      },
    });
  }

  viewDetails(row: EngagementSummaryDto): void {
    this.api.getEngagement(row.id).subscribe({
      next: (dto) => {
        this.selectedDetails.set(dto);
        this.detailsVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The engagement could not be loaded')),
    });
  }

  submitEngagement(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    this.api.create({
      legalEntityId: val.legalEntityId,
      title: val.title,
      clientId: val.clientId,
      clientName: val.clientName,
      type: val.type,
      contractValue: val.contractValue,
      budgetedHours: val.budgetedHours,
      startDate: toIsoDate(new Date(val.startDate)),
      targetEndDate: val.targetEndDate ? toIsoDate(new Date(val.targetEndDate)) : null,
      leadConsultantName: val.leadConsultantName || null,
      description: val.description || null,
    }).subscribe({
      next: () => {
        this.msg.success(doneText('created', 'Engagement', val.title));
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The engagement could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }
}

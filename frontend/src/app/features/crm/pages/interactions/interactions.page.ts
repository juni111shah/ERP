import { doneText } from '@core/format/done-text';
import { CommonModule } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '@core/api/admin.api';
import {
  CrmInteractionsApi,
  CrmLeadsApi,
  CrmOpportunitiesApi,
  CustomerInteractionDto,
  InteractionType
} from '@core/api/crm.api';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';

@Component({
  selector: 'erp-interactions-page',
  standalone: true,
  imports: [ErpLabelPipe, ErpDatePipe,
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <h1 class="erp-page-title">Customer Activity & Touchpoint Logs</h1>

    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search subject"
      [clickable]="false"
      emptyText="No activity yet. Log a call, meeting or note."
      exportFilename="crm-activity">
      
      <button toolbar *erpHasPermission="'crm.interactions.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="activity-log">
        <span nz-icon nzType="plus"></span> Log Activity
      </button>

      <ng-template #cell let-row let-column="column">
        <ng-container [ngSwitch]="column.key">
          <span *ngSwitchCase="'type'">
            <nz-tag [nzColor]="getTypeColor(row.type)">
              <span nz-icon [nzType]="getTypeIcon(row.type)"></span> {{ row.type | erpLabel }}
            </nz-tag>
          </span>
          <span *ngSwitchCase="'subject'">
            <strong class="subject">{{ row.subject }}</strong>
          </span>
          <span *ngSwitchCase="'relatedEntityKind'">
            <span class="related">{{ relatedLabel(row) }}</span>
          </span>
          <span *ngSwitchCase="'performedByName'">
            <span>{{ row.performedByName || '-' }}</span>
          </span>
          <span *ngSwitchCase="'interactionDate'">
            <span class="nowrap">{{ row.interactionDate | erpDate }}</span>
          </span>
          <span *ngSwitchCase="'followUpDate'">
            <span *ngIf="row.followUpDate" class="nowrap">{{ row.followUpDate | erpDate }}</span>
            <span *ngIf="!row.followUpDate" class="text-secondary">-</span>
          </span>
          <span *ngSwitchCase="'isResolved'">
            <nz-tag [nzColor]="row.isResolved ? 'success' : 'processing'">
              {{ row.isResolved ? 'Resolved' : (row.followUpDate ? 'Follow-up due' : 'Open') }}
            </nz-tag>
          </span>
          <span *ngSwitchCase="'actions'">
            <div class="table-actions">
              @if (!row.isResolved) {
                <button
                  *erpHasPermission="'crm.interactions.manage'"
                  nz-button nzType="link" nzSize="small"
                  (click)="resolveInteraction(row)">
                  <span nz-icon nzType="check"></span> Mark Done
                </button>
              }
            </div>
          </span>
        </ng-container>
      </ng-template>
    </erp-paged-table>

    <!-- Log Activity Drawer -->
    <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="560"
        nzTitle="Log Activity"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="activityForm" nz-form nzLayout="vertical" (ngSubmit)="saveActivity()">
            <nz-form-item>
              <nz-form-label nzRequired>Legal Entity</nz-form-label>
              <nz-form-control nzErrorTip="Entity required">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                  <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Interaction Type</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="call" nzLabel="Phone Call"></nz-option>
                    <nz-option nzValue="meeting" nzLabel="Meeting / Presentation"></nz-option>
                    <nz-option nzValue="email" nzLabel="Email Exchange"></nz-option>
                    <nz-option nzValue="note" nzLabel="Internal Note"></nz-option>
                    <nz-option nzValue="task" nzLabel="Action Task"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Related Context</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="relatedEntityKind" (ngModelChange)="onRelatedKindChange($event)">
                    <nz-option nzValue="Lead" nzLabel="Lead Prospect"></nz-option>
                    <nz-option nzValue="Opportunity" nzLabel="Pipeline Deal"></nz-option>
                    <nz-option nzValue="Customer" nzLabel="Account / Client"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Related Record</nz-form-label>
                <nz-form-control nzErrorTip="Please select a related record">
                  <nz-select formControlName="relatedEntityId" nzShowSearch nzPlaceHolder="Select record">
                    @for (r of relatedOptions(); track r.id) {
                      <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Subject / Topic</nz-form-label>
              <nz-form-control nzErrorTip="Subject is required">
                <input nz-input formControlName="subject" placeholder="e.g. Follow-up call about the proposal" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="details" rows="4" placeholder="What was discussed and what happens next..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Interaction Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="interactionDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Follow-Up Deadline</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="followUpDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Log Activity</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: var(--erp-color-text-secondary); }
    .nowrap { white-space: nowrap; }
    .subject { display: block; width: 280px; white-space: normal; }
    .related { display: block; width: 210px; white-space: normal; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .mb-4 { margin-bottom: 1rem; }
  `]
})
export class InteractionsPageComponent implements OnInit {
  private readonly api = inject(CrmInteractionsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly leadsApi = inject(CrmLeadsApi);
  private readonly oppsApi = inject(CrmOpportunitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<CustomerInteractionDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly leads = signal<{ id: string; name: string }[]>([]);
  readonly opportunities = signal<{ id: string; name: string }[]>([]);
  readonly customers = signal<{ id: string; name: string }[]>([]);

  readonly currentRelatedKind = signal<string>('Lead');
  readonly relatedOptions = computed(() => {
    const kind = this.currentRelatedKind();
    if (kind === 'Lead') return this.leads();
    if (kind === 'Opportunity') return this.opportunities();
    if (kind === 'Customer') return this.customers();
    return [];
  });

  readonly createVisible = signal(false);
  readonly isSubmitting = signal(false);

  activityForm!: FormGroup;

  readonly columns: TableColumn<CustomerInteractionDto>[] = [
    { key: 'type', label: 'Activity Type', sortField: 'type', width: '130px', filterField: 'type', filterOptions: ['call', 'meeting', 'email', 'note', 'task'] },
    { key: 'subject', label: 'Subject', sortField: 'subject' },
    { key: 'relatedEntityKind', label: 'Related To', filter: 'text', filterValue: (r) => this.relatedLabel(r), value: (r) => this.relatedLabel(r) },
    { key: 'performedByName', label: 'Logged By', hidden: true },
    { key: 'interactionDate', label: 'Date', sortField: 'interactionDate', width: '120px' },
    { key: 'followUpDate', label: 'Follow-Up', sortField: 'followUpDate', width: '120px' },
    { key: 'isResolved', label: 'Status', width: '120px', filter: false, value: (r) => (r.isResolved ? 'Resolved' : r.followUpDate ? 'Follow-up due' : 'Open') },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  ngOnInit(): void {
    this.initForm();
    this.loadEntities();
    this.loadRelatedData();
  }

  private initForm(): void {
    this.activityForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      relatedEntityKind: ['Lead', [Validators.required]],
      relatedEntityId: ['', [Validators.required]],
      type: ['call', [Validators.required]],
      subject: ['', [Validators.required]],
      details: [''],
      interactionDate: [new Date()],
      followUpDate: [new Date(Date.now() + 3 * 86400000)]
    });
  }

  private loadEntities(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.entities.set(res.items.map(e => ({ id: e.id, name: e.name })));
          if (res.items.length > 0) {
            this.activityForm.patchValue({ legalEntityId: res.items[0].id });
          }
        }
      }
    });
  }

  private loadRelatedData(): void {
    this.leadsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const items = (res.items || []).map((l: any) => ({
          id: l.id,
          name: l.fullName || l.companyName || l.leadNumber || l.id,
        }));
        this.leads.set(items);
        if (items.length > 0 && !this.activityForm.get('relatedEntityId')?.value) {
          this.activityForm.patchValue({ relatedEntityId: items[0].id });
        }
      },
      error: () => {},
    });

    this.oppsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const items = (res.items || []).map((o: any) => ({
          id: o.id,
          name: o.title || o.opportunityNumber || o.id,
        }));
        this.opportunities.set(items);
      },
      error: () => {},
    });

    this.partiesApi.list({ page: 1, pageSize: 100, extra: { type: 'customer' } }).subscribe({
      next: (res) => {
        const items = (res.items || []).map((p: any) => ({
          id: p.id,
          name: p.name || p.id,
        }));
        this.customers.set(items);
      },
      error: () => {},
    });
  }

  /** The kind, with the name of the record when it is one of those loaded for the form. */
  relatedLabel(row: CustomerInteractionDto): string {
    const pool = row.relatedEntityKind === 'Lead' ? this.leads() : row.relatedEntityKind === 'Opportunity' ? this.opportunities() : this.customers();
    const name = pool.find((r) => r.id === row.relatedEntityId)?.name;
    return name ? `${row.relatedEntityKind}: ${name}` : row.relatedEntityKind;
  }

  onRelatedKindChange(kind: string): void {
    this.currentRelatedKind.set(kind);
    let defaultId = '';
    if (kind === 'Lead') defaultId = this.leads()[0]?.id || '';
    else if (kind === 'Opportunity') defaultId = this.opportunities()[0]?.id || '';
    else if (kind === 'Customer') defaultId = this.customers()[0]?.id || '';
    this.activityForm.patchValue({ relatedEntityId: defaultId });
  }

  getTypeColor(type: InteractionType): string {
    switch (type) {
      case 'call': return 'blue';
      case 'meeting': return 'purple';
      case 'email': return 'orange';
      case 'note': return 'cyan';
      case 'task': return 'geekblue';
      default: return 'default';
    }
  }

  getTypeIcon(type: InteractionType): string {
    switch (type) {
      case 'call': return 'phone';
      case 'meeting': return 'team';
      case 'email': return 'mail';
      case 'note': return 'file-text';
      case 'task': return 'check-square';
      default: return 'message';
    }
  }

  openCreateDrawer(): void {
    this.currentRelatedKind.set('Lead');
    const defaultLeadId = this.leads()[0]?.id || '';
    this.activityForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      relatedEntityKind: 'Lead',
      relatedEntityId: defaultLeadId,
      type: 'call',
      interactionDate: new Date(),
      followUpDate: new Date(Date.now() + 3 * 86400000)
    });
    this.createVisible.set(true);
  }

  resolveInteraction(row: CustomerInteractionDto): void {
    this.api.resolve(row.id).subscribe({
      next: () => {
        this.message.success(doneText('completed', 'Activity', row.subject));
        this.list.reload();
      },
      error: () => this.message.error('Failed to resolve interaction')
    });
  }

  saveActivity(): void {
    if (this.activityForm.invalid) {
      Object.values(this.activityForm.controls).forEach(c => { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); });
      return;
    }
    const v = this.activityForm.value;
    if (!v.relatedEntityId) {
      this.message.error('Please select a related record.');
      return;
    }
    this.isSubmitting.set(true);

    const payload = {
      legalEntityId: v.legalEntityId,
      relatedEntityKind: v.relatedEntityKind,
      relatedEntityId: v.relatedEntityId,
      type: v.type,
      subject: v.subject,
      details: v.details,
      interactionDate: v.interactionDate ? new Date(v.interactionDate).toISOString() : new Date().toISOString(),
      followUpDate: v.followUpDate ? new Date(v.followUpDate).toISOString() : null,
      isResolved: false
    };

    this.api.create(payload as any).subscribe({
      next: () => {
        this.message.success(doneText('recorded', 'Activity', v.subject));
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'Failed to log activity'));
        this.isSubmitting.set(false);
      }
    });
  }
}

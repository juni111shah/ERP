import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
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
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CrmInteractionsApi,
  CustomerInteractionDto,
  InteractionType
} from '../../core/api/crm.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-interactions-page',
  standalone: true,
  imports: [
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
    PagedTableComponent
  ],
  template: `
    <h1 class="erp-page-title">Customer Activity & Touchpoint Logs</h1>

    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns">
      
      <button toolbar nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="activity-log">
        <span nz-icon nzType="plus"></span> Log Activity
      </button>

      <ng-template #cell let-row let-column="column">
        <ng-container [ngSwitch]="column.key">
          <span *ngSwitchCase="'type'">
            <nz-tag [nzColor]="getTypeColor(row.type)">
              <span nz-icon [nzType]="getTypeIcon(row.type)"></span> {{ row.type }}
            </nz-tag>
          </span>
          <span *ngSwitchCase="'subject'">
            <strong>{{ row.subject }}</strong>
          </span>
          <span *ngSwitchCase="'relatedEntityKind'">
            <nz-tag>{{ row.relatedEntityKind }}</nz-tag>
          </span>
          <span *ngSwitchCase="'performedByName'">
            <span>{{ row.performedByName || 'Agent' }}</span>
          </span>
          <span *ngSwitchCase="'interactionDate'">
            <small>{{ row.interactionDate | date:'mediumDate' }}</small>
          </span>
          <span *ngSwitchCase="'followUpDate'">
            <small *ngIf="row.followUpDate" class="text-warning font-medium">
              {{ row.followUpDate | date:'mediumDate' }}
            </small>
            <span *ngIf="!row.followUpDate" class="text-secondary">-</span>
          </span>
          <span *ngSwitchCase="'isResolved'">
            <nz-tag [nzColor]="row.isResolved ? 'success' : 'processing'">
              {{ row.isResolved ? 'Resolved' : 'Follow-up Due' }}
            </nz-tag>
          </span>
          <span *ngSwitchCase="'actions'">
            <div class="table-actions">
              <button
                *ngIf="!row.isResolved"
                nz-button nzType="link" nzSize="small" class="text-success"
                (click)="resolveInteraction(row)">
                <span nz-icon nzType="check"></span> Mark Done
              </button>
            </div>
          </span>
        </ng-container>
      </ng-template>
    </erp-paged-table>

    <!-- Log Activity Drawer -->
    <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="560"
        nzTitle="Log Customer Interaction"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="activityForm" nz-form nzLayout="vertical" (ngSubmit)="saveActivity()">
            <nz-form-item>
              <nz-form-label nzRequired>Workspace Legal Entity</nz-form-label>
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
                    <nz-option nzValue="Call" nzLabel="Phone Call"></nz-option>
                    <nz-option nzValue="Meeting" nzLabel="Meeting / Presentation"></nz-option>
                    <nz-option nzValue="Email" nzLabel="Email Exchange"></nz-option>
                    <nz-option nzValue="Note" nzLabel="Internal Note"></nz-option>
                    <nz-option nzValue="Task" nzLabel="Action Task"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Related Context</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="relatedEntityKind">
                    <nz-option nzValue="Lead" nzLabel="Lead Prospect"></nz-option>
                    <nz-option nzValue="Opportunity" nzLabel="Pipeline Deal"></nz-option>
                    <nz-option nzValue="Customer" nzLabel="Account / Client"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Subject / Topic</nz-form-label>
              <nz-form-control nzErrorTip="Subject is required">
                <input nz-input formControlName="subject" placeholder="e.g. Discovery demo follow-up call with CFO" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Detailed Notes & Summary</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="details" rows="4" placeholder="Client questions, objections discussed, agreed next actions..."></textarea>
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
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Log Touchpoint</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-warning { color: #f59e0b; }
    .text-success { color: #10b981; }
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
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<CustomerInteractionDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);

  readonly createVisible = signal(false);
  readonly isSubmitting = signal(false);

  activityForm!: FormGroup;

  readonly columns: TableColumn<CustomerInteractionDto>[] = [
    { key: 'type', label: 'Activity Type', sortField: 'type' },
    { key: 'subject', label: 'Subject / Meeting Topic', sortField: 'subject' },
    { key: 'relatedEntityKind', label: 'Related To' },
    { key: 'performedByName', label: 'Rep / Agent' },
    { key: 'interactionDate', label: 'Interaction Date' },
    { key: 'followUpDate', label: 'Follow-Up' },
    { key: 'isResolved', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  ngOnInit(): void {
    this.initForm();
    this.loadEntities();
  }

  private initForm(): void {
    this.activityForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      relatedEntityKind: ['Lead', [Validators.required]],
      type: ['Call', [Validators.required]],
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

  getTypeColor(type: InteractionType): string {
    switch (type) {
      case 'Call': return 'blue';
      case 'Meeting': return 'purple';
      case 'Email': return 'orange';
      case 'Note': return 'cyan';
      case 'Task': return 'geekblue';
      default: return 'default';
    }
  }

  getTypeIcon(type: InteractionType): string {
    switch (type) {
      case 'Call': return 'phone';
      case 'Meeting': return 'team';
      case 'Email': return 'mail';
      case 'Note': return 'file-text';
      case 'Task': return 'check-square';
      default: return 'message';
    }
  }

  openCreateDrawer(): void {
    this.activityForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      relatedEntityKind: 'Lead',
      type: 'Call',
      interactionDate: new Date(),
      followUpDate: new Date(Date.now() + 3 * 86400000)
    });
    this.createVisible.set(true);
  }

  resolveInteraction(row: CustomerInteractionDto): void {
    this.api.resolve(row.id).subscribe({
      next: () => {
        this.message.success('Follow-up marked resolved');
        this.list.reload();
      },
      error: () => this.message.error('Failed to resolve interaction')
    });
  }

  saveActivity(): void {
    if (this.activityForm.invalid) {
      Object.values(this.activityForm.controls).forEach(c => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.activityForm.value;

    const payload = {
      legalEntityId: v.legalEntityId,
      relatedEntityKind: v.relatedEntityKind,
      relatedEntityId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      type: v.type,
      subject: v.subject,
      details: v.details,
      interactionDate: v.interactionDate ? new Date(v.interactionDate).toISOString() : new Date().toISOString(),
      followUpDate: v.followUpDate ? new Date(v.followUpDate).toISOString() : null,
      isResolved: false
    };

    this.api.create(payload as any).subscribe({
      next: () => {
        this.message.success('Activity touchpoint logged');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: () => {
        this.message.error('Failed to log activity');
        this.isSubmitting.set(false);
      }
    });
  }
}

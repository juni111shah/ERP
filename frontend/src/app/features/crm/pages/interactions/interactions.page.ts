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
  templateUrl: './interactions.page.html',
  styleUrl: './interactions.page.scss'
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

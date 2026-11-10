import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
import { CrmInteractionsApi, CrmLeadsApi, CrmOpportunitiesApi, CustomerInteractionDto, InteractionType } from '@core/api/crm.api';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { InteractionFormValue, interactionColor, interactionIcon, toInteractionInput } from '../../crm-interactions';
import { CrmLookups, NamedOption, daysFromNow, leadOption, opportunityOption, partyOption } from '../../crm-lookups';

/** Customer activities (calls, meetings, emails...): the paged list, logging one against a lead, deal or customer, and resolving it. */
@Component({
  selector: 'erp-interactions-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzBadgeModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    ErpDatePipe,
    ErpLabelPipe,
    HasPermissionDirective,
    PagedTableComponent,
  ],
  providers: [CrmLookups],
  templateUrl: './interactions.page.html',
  styleUrl: './interactions.page.scss'
})
export class InteractionsPageComponent implements OnInit {
  private readonly api = inject(CrmInteractionsApi);
  private readonly leadsApi = inject(CrmLeadsApi);
  private readonly oppsApi = inject(CrmOpportunitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly lookups = inject(CrmLookups);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<CustomerInteractionDto>((q) => this.api.list(q));
  readonly entities = this.lookups.entities;
  readonly leads = signal<NamedOption[]>([]);
  readonly opportunities = signal<NamedOption[]>([]);
  readonly customers = signal<NamedOption[]>([]);

  readonly currentRelatedKind = signal<string>('Lead');
  readonly relatedOptions = computed(() => this.optionsOf(this.currentRelatedKind()));

  readonly createVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly activityForm: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    relatedEntityKind: ['Lead', [Validators.required]],
    relatedEntityId: ['', [Validators.required]],
    type: ['call', [Validators.required]],
    subject: ['', [Validators.required]],
    details: [''],
    interactionDate: [new Date()],
    followUpDate: [daysFromNow(3)]
  });

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
    this.lookups.loadEntities(this.activityForm);
    this.loadRelatedData();
  }

  /** The kind, with the name of the record when it is one of those loaded for the form. */
  relatedLabel(row: CustomerInteractionDto): string {
    const pool = row.relatedEntityKind === 'Lead' ? this.leads() : row.relatedEntityKind === 'Opportunity' ? this.opportunities() : this.customers();
    const name = pool.find((r) => r.id === row.relatedEntityId)?.name;
    return name ? `${row.relatedEntityKind}: ${name}` : row.relatedEntityKind;
  }

  onRelatedKindChange(kind: string): void {
    this.currentRelatedKind.set(kind);
    this.activityForm.patchValue({ relatedEntityId: this.optionsOf(kind)[0]?.id || '' });
  }

  getTypeColor(type: InteractionType): string {
    return interactionColor(type);
  }

  getTypeIcon(type: InteractionType): string {
    return interactionIcon(type);
  }

  openCreateDrawer(): void {
    this.currentRelatedKind.set('Lead');
    this.activityForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      relatedEntityKind: 'Lead',
      relatedEntityId: this.leads()[0]?.id || '',
      type: 'call',
      interactionDate: new Date(),
      followUpDate: daysFromNow(3)
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
      revealErrors(this.activityForm, { onlySelf: true });
      return;
    }
    const v = this.activityForm.value as InteractionFormValue;
    if (!v.relatedEntityId) {
      this.message.error('Please select a related record.');
      return;
    }
    this.isSubmitting.set(true);

    this.api.create(toInteractionInput(v)).subscribe({
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

  /** The records an activity of this kind can be logged against. */
  private optionsOf(kind: string): NamedOption[] {
    if (kind === 'Lead') return this.leads();
    if (kind === 'Opportunity') return this.opportunities();
    if (kind === 'Customer') return this.customers();
    return [];
  }

  /** The leads (the first becomes the default related record), deals and customers an activity can be logged against. */
  private loadRelatedData(): void {
    this.leadsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const items = (res.items || []).map(leadOption);
        this.leads.set(items);
        if (items.length > 0 && !this.activityForm.get('relatedEntityId')?.value) {
          this.activityForm.patchValue({ relatedEntityId: items[0].id });
        }
      },
      error: () => {},
    });
    loadInto(this.opportunities, this.oppsApi.list({ page: 1, pageSize: 100 }), (res) => (res.items || []).map(opportunityOption));
    loadInto(this.customers, this.partiesApi.list({ page: 1, pageSize: 100, extra: { type: 'customer' } }), (res) => (res.items || []).map(partyOption));
  }
}

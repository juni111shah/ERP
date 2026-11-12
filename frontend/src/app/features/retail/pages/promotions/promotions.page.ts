import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { createPagedList } from '@core/api/paged-list';
import { CreatePromotionInput, PromotionType, PromoValidationResultDto, RetailPromotionDto, RetailPromotionsApi } from '@core/api/retail.api';
import { toIsoDate } from '@core/format/dates';
import { displayLabel } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { defaultEntityId, localDay, notBeforeStart, PromotionFormValue, revealInvalid } from '../../retail-forms';
import { RetailLookups } from '../../retail-lookups';

const DAY_MS = 86400000;

/** The types this form can create; any other is kept only when editing a promotion that already has it. */
const FORM_TYPES: PromotionType[] = ['percentageDiscount', 'fixedAmountDiscount'];

/** What the promotion is doing today: the switch alone does not say whether it has ended or used up its limit. */
function promoStatus(row: RetailPromotionDto): string {
  if (!row.isActive) return 'inactive';
  const today = toIsoDate(new Date());
  if (row.startDate > today) return 'scheduled';
  if (row.endDate < today) return 'expired';
  if (row.usageLimit && row.timesUsed >= row.usageLimit) return 'limitReached';
  return 'active';
}

/** The API body from the form: the code in capitals, dates as the local calendar days picked. */
function toPromotionInput(val: PromotionFormValue, customFields: string | null): CreatePromotionInput {
  return {
    legalEntityId: val.legalEntityId,
    code: val.code.toUpperCase(),
    name: val.name,
    type: val.type,
    discountValue: val.discountValue,
    minimumOrderAmount: val.minimumOrderAmount,
    startDate: toIsoDate(new Date(val.startDate)),
    endDate: toIsoDate(new Date(val.endDate)),
    usageLimit: val.usageLimit,
    isActive: val.isActive,
    description: val.description,
    customFields,
  };
}

/** Retail promotions: the paged list, the create/edit modal and a simulator that tries a code on an order total. */
@Component({
  selector: 'erp-promotions-page',
  standalone: true,
  imports: [ErpDatePipe, ErpLabelPipe, NzPopconfirmModule, StatusTagComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzSwitchModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    NzAlertModule,
    NzDividerModule,
    NzProgressModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  providers: [RetailLookups],
  templateUrl: './promotions.page.html',
  styleUrl: './promotions.page.scss'
})
export class PromotionsPageComponent implements OnInit {
  private readonly promotionsApi = inject(RetailPromotionsApi);
  private readonly lookups = inject(RetailLookups);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = this.lookups.entities;
  readonly isSaving = signal(false);
  readonly editingId = signal<string | null>(null);
  /** A type this form cannot create, kept selectable when editing a promotion that has it. */
  readonly otherType = signal<PromotionType | null>(null);
  private editingCustomFields: string | null = null;
  readonly isSimulating = signal(false);
  readonly simResult = signal<PromoValidationResultDto | null>(null);

  readonly isModalVisible = signal(false);
  readonly isSimulatorVisible = signal(false);
  simCode = '';
  simTotal = 250;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['percentageDiscount', [Validators.required]],
    discountValue: [10, [Validators.required, Validators.min(0.01)]],
    minimumOrderAmount: [null],
    startDate: [new Date(), [Validators.required]],
    endDate: [new Date(Date.now() + 30 * DAY_MS), [Validators.required, notBeforeStart('startDate')]],
    usageLimit: [null],
    isActive: [true],
    description: ['']
  });

  readonly columns: TableColumn<RetailPromotionDto>[] = [
    { key: 'code', label: 'Promo code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type', hidden: true },
    { key: 'discountValue', label: 'Discount', align: 'right' },
    { key: 'validity', label: 'Valid', sortField: 'startDate', filter: false, value: (r) => `${r.startDate} to ${r.endDate}` },
    { key: 'usage', label: 'Times used', filter: false, value: (r) => (r.usageLimit ? `${r.timesUsed} of ${r.usageLimit}` : r.timesUsed) },
    { key: 'status', label: 'Status', value: (r) => displayLabel(this.promoStatus(r)) },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailPromotionDto>((q) => this.promotionsApi.list(q), { sort: '-createdAt' });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('endDate')!.updateValueAndValidity());
    this.lookups.loadEntities();
  }

  /** What the promotion is doing today: the switch alone does not say whether it has ended or used up its limit. */
  promoStatus(row: RetailPromotionDto): string {
    return promoStatus(row);
  }

  getUsagePercent(used: number, limit: number): number {
    if (!limit || limit <= 0) return 0;
    return Math.min(100, Math.round((used / limit) * 100));
  }

  openEditModal(row: RetailPromotionDto): void {
    this.editingId.set(row.id);
    this.editingCustomFields = row.customFields ?? null;
    this.otherType.set(FORM_TYPES.includes(row.type) ? null : row.type);
    this.form.reset({
      legalEntityId: row.legalEntityId,
      code: row.code,
      name: row.name,
      type: row.type,
      discountValue: row.discountValue,
      minimumOrderAmount: row.minimumOrderAmount ?? null,
      startDate: localDay(row.startDate),
      endDate: localDay(row.endDate),
      usageLimit: row.usageLimit ?? null,
      isActive: row.isActive,
      description: row.description ?? ''
    });
    this.form.get('legalEntityId')!.disable({ emitEvent: false });
    this.isModalVisible.set(true);
  }

  deletePromotion(row: RetailPromotionDto): void {
    this.promotionsApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success(doneText('deleted', 'Promotion', row.name));
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The promotion could not be deleted'))
    });
  }

  openCreateModal(): void {
    this.editingId.set(null);
    this.editingCustomFields = null;
    this.otherType.set(null);
    this.form.get('legalEntityId')!.enable({ emitEvent: false });
    this.form.reset({
      legalEntityId: defaultEntityId(this.entities(), this.sessionStore.currentEntityId()),
      code: '',
      name: '',
      type: 'percentageDiscount',
      discountValue: 10,
      minimumOrderAmount: null,
      startDate: new Date(),
      endDate: new Date(Date.now() + 30 * DAY_MS),
      usageLimit: null,
      isActive: true,
      description: ''
    });
    this.isModalVisible.set(true);
  }

  closeModal(): void {
    this.isModalVisible.set(false);
  }

  openSimulatorModal(): void {
    this.simResult.set(null);
    this.isSimulatorVisible.set(true);
  }

  testCode(code: string): void {
    this.simCode = code;
    this.openSimulatorModal();
    this.runSimulation();
  }

  runSimulation(): void {
    if (!this.simCode) return;
    const entityId = this.entities()[0]?.id;
    if (!entityId) {
      this.msg.warning('No active workspace entity found');
      return;
    }

    this.isSimulating.set(true);
    this.promotionsApi.validateCode(entityId, this.simCode, this.simTotal).subscribe({
      next: (res) => {
        this.simResult.set(res);
        this.isSimulating.set(false);
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The code could not be tested'));
        this.isSimulating.set(false);
      }
    });
  }

  savePromotion(): void {
    if (this.form.invalid) {
      revealInvalid(this.form);
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue() as PromotionFormValue;
    const input = toPromotionInput(val, this.editingCustomFields);

    const id = this.editingId();
    (id ? this.promotionsApi.update(id, input) : this.promotionsApi.create(input)).subscribe({
      next: () => {
        this.msg.success(doneText('saved', 'Promotion', val.name));
        this.isSaving.set(false);
        this.closeModal();
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The promotion could not be saved'));
        this.isSaving.set(false);
      }
    });
  }
}

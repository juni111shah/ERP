import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { BomComponentDto, ManufacturingBomsApi, ManufacturingWorkOrdersApi, WorkOrderDto, WorkOrderOperationDto } from '@core/api/manufacturing.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ActionFormState } from '@shared/utils/action-form-state';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { askReason } from '@shared/utils/reason-prompt';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

const money = (v: number) => new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);

/** One work order: cost so far, the material put into it, the hours worked per operation, and the steps that move it along or undo it. */
@Component({
  selector: 'erp-work-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzCardModule, NzInputNumberModule, NzModalModule, NzSpinModule, NzTableModule, DetailPageComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styleUrl: './work-order-detail.page.scss',
  templateUrl: './work-order-detail.page.html',
})
export class WorkOrderDetailPage implements OnInit {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly boms = inject(ManufacturingBomsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly fmt = (v: unknown) => displayDate(v);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly order = signal<WorkOrderDto | null>(null);
  readonly components = signal<BomComponentDto[]>([]);
  private readonly hours = signal<Record<string, number>>({});

  readonly issue = new ActionFormState({
    run: (p) => this.api.issueMaterials(this.order()!.id, { lines: this.linesOf(p), date: this.dateOf(p), notes: (p['notes'] as string) || null }),
    success: () => doneText('recorded', 'Material issue', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not issue the material',
  });
  readonly giveBack = new ActionFormState({
    run: (p) => this.api.returnMaterials(this.order()!.id, { lines: this.linesOf(p) ?? [], date: this.dateOf(p), notes: (p['notes'] as string) || null }),
    success: () => doneText('recorded', 'Material return', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not return the material',
  });
  readonly complete = new ActionFormState({
    run: (p) => this.api.complete(this.order()!.id, {
      completedQuantity: Number(p['completedQuantity'] ?? 0), scrappedQuantity: Number(p['scrappedQuantity'] ?? 0), actualTotalCost: this.order()!.estimatedTotalCost,
      completionDate: this.dateOf(p), notes: (p['notes'] as string) || null,
      operationHours: this.order()!.operations.filter((op) => this.hours()[op.id] !== undefined).map((op) => ({ operationId: op.id, actualHours: this.hours()[op.id] })),
    }),
    success: () => doneText('completed', 'Work order', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not complete the work order',
  });

  private readonly session = inject(SessionStore);
  readonly canManage = () => this.session.hasPermission('manufacturing.workorders.manage');
  private readonly itemOptions = computed(() => this.components().map((c) => ({ value: c.componentItemId, label: `${c.componentItemCode} · ${c.componentItemName}` })));
  readonly issueFields = computed<FieldSpec[]>(() => [
    { key: 'lines', label: 'Material', type: 'rows', columns: [{ key: 'itemId', label: 'Item', type: 'select', options: this.itemOptions() }, { key: 'quantity', label: 'Quantity', type: 'number', width: '110px' }], rowDefaults: { itemId: '', quantity: 1 }, hint: 'Leave empty to issue what the bill of materials still needs for the planned quantity.' },
    { key: 'date', label: 'Date', type: 'date', hint: 'Left empty, today. It cannot be in the future.' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ]);
  readonly returnFields = computed<FieldSpec[]>(() => [
    { key: 'lines', label: 'Material to return', type: 'rows', required: true, columns: [{ key: 'itemId', label: 'Item', type: 'select', options: this.issuedOptions() }, { key: 'quantity', label: 'Quantity', type: 'number', width: '110px' }], rowDefaults: { itemId: '', quantity: 1 }, hint: 'Material goes back to stock at the cost it was issued at, and no more than is on the order can be returned.' },
    { key: 'date', label: 'Date', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ]);
  private readonly issuedOptions = computed(() => {
    const seen = new Map<string, string>();
    for (const m of this.order()?.materialIssues ?? []) seen.set(m.itemId, `${m.itemCode} · ${m.itemName}`);
    return [...seen].map(([value, label]) => ({ value, label }));
  });
  readonly completeFields = computed<FieldSpec[]>(() => [
    { key: 'completedQuantity', label: 'Quantity produced', type: 'number', required: true, min: 0, default: this.order()?.plannedQuantity ?? 0 },
    { key: 'scrappedQuantity', label: 'Quantity scrapped', type: 'number', min: 0, default: 0 },
    { key: 'completionDate', label: 'Completion date', type: 'date', hint: 'The day the goods came out of production. Left empty, today. It cannot be in the future.' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ]);

  readonly actions = computed((): ActionButton[] => {
    const o = this.order();
    if (!o) return [];
    return [
      { label: 'Release', icon: 'play-circle', type: 'primary', permission: 'manufacturing.workorders.manage', visible: o.status === 'planned', handler: () => this.release() },
      { label: 'Issue material', icon: 'export', permission: 'manufacturing.workorders.manage', visible: o.status === 'inProgress', handler: () => this.issue.show() },
      { label: 'Return material', icon: 'rollback', permission: 'manufacturing.workorders.manage', visible: o.status === 'inProgress' && this.issuedOptions().length > 0, handler: () => this.giveBack.show() },
      { label: 'Complete', icon: 'check-circle', type: 'primary', permission: 'manufacturing.workorders.manage', visible: o.status === 'inProgress', handler: () => this.complete.show() },
      { label: 'Reverse', icon: 'undo', danger: true, permission: 'manufacturing.workorders.reverse', visible: o.status === 'completed', handler: () => this.reverse() },
      { label: 'Cancel', icon: 'close-circle', danger: true, permission: 'manufacturing.workorders.manage', visible: (o.status === 'draft' || o.status === 'planned' || o.status === 'inProgress') && o.completedQuantity === 0, handler: () => this.cancel() },
    ];
  });

  readonly kpis = computed((): KpiCard[] => {
    const o = this.order();
    if (!o) return [];
    const cur = this.currency();
    return [
      { title: 'Produced', value: `${o.completedQuantity} of ${o.plannedQuantity}`, suffix: o.uom },
      { title: 'Material cost', value: money(o.materialCost), suffix: cur },
      { title: 'Conversion cost', value: money(o.conversionCost), suffix: cur },
      { title: 'Scrap cost', value: money(o.scrapCost), suffix: cur },
      { title: 'Estimated cost', value: money(o.estimatedTotalCost), suffix: cur },
    ];
  });

  readonly descriptions = computed((): DescriptionItem[] => {
    const o = this.order();
    if (!o) return [];
    return [
      { label: 'Bill of materials', value: o.bomCode }, { label: 'Start', value: o.startDate }, { label: 'Due', value: o.dueDate },
      { label: 'Completed on', value: o.completionDate ?? null }, { label: 'Scrapped', value: o.scrappedQuantity }, { label: 'Notes', value: o.notes ?? null, span: 2 },
    ];
  });

  ngOnInit(): void { this.load(); }

  load(showSpinner = true): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Invalid work order'); this.loading.set(false); return; }
    if (showSpinner) { this.loading.set(true); this.error.set(null); }
    this.api.detail(id).subscribe({
      next: (o) => {
        this.order.set(o);
        this.hours.set({});
        this.loading.set(false);
        if (!this.components().length) this.boms.getDetail(o.bomId).subscribe({ next: (b) => this.components.set(b.components), error: () => undefined });
      },
      error: (e) => { this.error.set(problemMessage(e, 'The work order could not be loaded')); this.loading.set(false); },
    });
  }

  hoursFor(op: WorkOrderOperationDto): number { return this.hours()[op.id] ?? op.actualHours; }
  setHours(id: string, v: number | null): void { this.hours.update((h) => ({ ...h, [id]: v ?? 0 })); }

  saveHours(op: WorkOrderOperationDto): void {
    const o = this.order();
    if (!o) return;
    this.api.recordHours(o.id, op.id, this.hoursFor(op)).subscribe({
      next: () => { this.message.success(doneText('recorded', 'Hours', op.operationName)); this.load(false); },
      error: (e) => this.message.error(problemMessage(e, 'Could not record the hours')),
    });
  }

  /** The rows of a material form as lines; null when none were entered. */
  private linesOf(p: Record<string, unknown>): { itemId: string; quantity: number }[] | undefined {
    const rows = ((p['lines'] as Record<string, unknown>[] | null) ?? []).filter((r) => r['itemId'] && Number(r['quantity']) > 0);
    return rows.length ? rows.map((r) => ({ itemId: String(r['itemId']), quantity: Number(r['quantity']) })) : undefined;
  }
  private dateOf(p: Record<string, unknown>): string | null { return (p['date'] as string) || (p['completionDate'] as string) || null; }

  private release(): void {
    const o = this.order();
    if (!o) return;
    this.api.release(o.id).subscribe({
      next: () => { this.message.success(doneText('released', 'Work order', o.workOrderNumber)); this.load(false); },
      error: (e) => this.message.error(problemMessage(e, 'The work order could not be released')),
    });
  }

  private reverse(): void {
    const o = this.order();
    if (!o) return;
    askReason(this.modal, `Reverse ${o.workOrderNumber}?`, 'Reverse work order', 'The finished goods are taken back out of stock, the material returns at its issue cost and every entry of the order is reversed today. This is refused if the finished goods have already left stock.').then((reason) => {
      if (!reason) return;
      this.api.reverse(o.id, reason).subscribe({
        next: () => { this.message.success(doneText('voided', 'Work order', o.workOrderNumber)); this.load(false); },
        error: (e) => this.message.error(problemMessage(e, 'The work order could not be reversed', 'reason')),
      });
    });
  }

  private cancel(): void {
    const o = this.order();
    if (!o) return;
    askReason(this.modal, `Cancel ${o.workOrderNumber}?`, 'Cancel work order', 'Material already issued goes back to stock.', false).then((reason) => {
      if (reason === null) return;
      this.api.cancel(o.id, reason).subscribe({
        next: () => { this.message.success(doneText('cancelled', 'Work order', o.workOrderNumber)); this.load(false); },
        error: (e) => this.message.error(problemMessage(e, 'The work order could not be cancelled')),
      });
    });
  }
}

import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { BomComponentDto, CompleteWorkOrderInput, ManufacturingBomsApi, ManufacturingWorkOrdersApi, WorkOrderDto, WorkOrderOperationDto } from '@core/api/manufacturing.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ActionFormState } from '@shared/utils/action-form-state';
import { loadInto } from '@shared/utils/load-into';
import { askReason } from '@shared/utils/reason-prompt';
import {
  DrawerPayload,
  completeFieldSpecs,
  componentItemOptions,
  formDateOf,
  formNotesOf,
  issueFieldSpecs,
  issuedItemOptions,
  materialLinesOf,
  returnFieldSpecs,
  workOrderDescriptions,
  workOrderKpis,
} from '../../manufacturing-work-order-view';
import { canCancelWorkOrder } from '../../manufacturing-work-orders';

const MANAGE = 'manufacturing.workorders.manage';

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
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly fmt = (v: unknown) => displayDate(v);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly order = signal<WorkOrderDto | null>(null);
  readonly components = signal<BomComponentDto[]>([]);
  private readonly hours = signal<Record<string, number>>({});

  readonly issue = new ActionFormState({
    run: (p) => this.api.issueMaterials(this.order()!.id, { lines: materialLinesOf(p), date: formDateOf(p), notes: formNotesOf(p) }),
    success: () => doneText('recorded', 'Material issue', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not issue the material',
  });
  readonly giveBack = new ActionFormState({
    run: (p) => this.api.returnMaterials(this.order()!.id, { lines: materialLinesOf(p) ?? [], date: formDateOf(p), notes: formNotesOf(p) }),
    success: () => doneText('recorded', 'Material return', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not return the material',
  });
  readonly complete = new ActionFormState({
    run: (p) => this.api.complete(this.order()!.id, this.completion(p)),
    success: () => doneText('completed', 'Work order', this.order()?.workOrderNumber), done: () => this.load(false), message: this.message, failure: 'Could not complete the work order',
  });

  readonly canManage = () => this.session.hasPermission(MANAGE);
  private readonly issuedOptions = computed(() => issuedItemOptions(this.order()));
  readonly issueFields = computed<FieldSpec[]>(() => issueFieldSpecs(componentItemOptions(this.components())));
  readonly returnFields = computed<FieldSpec[]>(() => returnFieldSpecs(this.issuedOptions()));
  readonly completeFields = computed<FieldSpec[]>(() => completeFieldSpecs(this.order()));

  readonly actions = computed((): ActionButton[] => {
    const o = this.order();
    if (!o) return [];
    return [
      { label: 'Release', icon: 'play-circle', type: 'primary', permission: MANAGE, visible: o.status === 'planned', handler: () => this.release() },
      { label: 'Issue material', icon: 'export', permission: MANAGE, visible: o.status === 'inProgress', handler: () => this.issue.show() },
      { label: 'Return material', icon: 'rollback', permission: MANAGE, visible: o.status === 'inProgress' && this.issuedOptions().length > 0, handler: () => this.giveBack.show() },
      { label: 'Complete', icon: 'check-circle', type: 'primary', permission: MANAGE, visible: o.status === 'inProgress', handler: () => this.complete.show() },
      { label: 'Reverse', icon: 'undo', danger: true, permission: 'manufacturing.workorders.reverse', visible: o.status === 'completed', handler: () => this.reverse() },
      { label: 'Cancel', icon: 'close-circle', danger: true, permission: MANAGE, visible: canCancelWorkOrder(o), handler: () => this.cancel() },
    ];
  });

  readonly kpis = computed((): KpiCard[] => {
    const o = this.order();
    return o ? workOrderKpis(o, this.currency()) : [];
  });

  readonly descriptions = computed((): DescriptionItem[] => {
    const o = this.order();
    return o ? workOrderDescriptions(o) : [];
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
        if (!this.components().length) loadInto(this.components, this.boms.getDetail(o.bomId), (b) => b.components);
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

  /** The completion body: the quantities, the estimate as the cost (the server works out the actual), and the hours typed per operation. */
  private completion(p: DrawerPayload): CompleteWorkOrderInput {
    const o = this.order()!;
    return {
      completedQuantity: Number(p['completedQuantity'] ?? 0), scrappedQuantity: Number(p['scrappedQuantity'] ?? 0), actualTotalCost: o.estimatedTotalCost,
      completionDate: formDateOf(p), notes: formNotesOf(p),
      operationHours: o.operations.filter((op) => this.hours()[op.id] !== undefined).map((op) => ({ operationId: op.id, actualHours: this.hours()[op.id] })),
    };
  }

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

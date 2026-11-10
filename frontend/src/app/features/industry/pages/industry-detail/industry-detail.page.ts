import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { Observable, catchError, of } from 'rxjs';
import { TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { IndustryApi, IndustryRecordDto, PackView, RecordTransitionDef, RecordTypeView, statusLabel, toneColor } from '@core/api/industry.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { displayDate, displayMoney } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { ActionButton, DescriptionItem, DetailPageComponent } from '@shared/components/detail-page/detail-page.component';
import { askReason } from '@shared/utils/reason-prompt';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { IndustryRecordFormComponent } from '../../components/industry-record-form/industry-record-form.component';

type Step = 'invoice' | 'bill' | 'consume';

/** One record of any type: its fields, lines, history and the next steps the server allows. Every module and type uses this page. */
@Component({
  selector: 'erp-industry-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterLink, TranslatePipe, NzAlertModule, NzButtonModule, NzCheckboxModule, NzInputModule, NzInputNumberModule, NzModalModule, NzProgressModule,
    NzSelectModule, NzSkeletonModule, NzTableModule, NzTagModule, NzTimelineModule, DetailPageComponent, StatePanelComponent, IndustryRecordFormComponent],
  styleUrl: './industry-detail.page.scss',
  templateUrl: './industry-detail.page.html',
})
export class IndustryDetailPage {
  private readonly api = inject(IndustryApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  private readonly currency = inject(WorkspaceCurrencyService);
  private readonly taxApi = inject(TaxCodesApi);
  private readonly warehouseApi = inject(WarehousesApi);

  readonly module = signal(this.route.snapshot.paramMap.get('module') ?? '');
  readonly typeKey = signal(this.route.snapshot.paramMap.get('type') ?? '');
  readonly id = signal(this.route.snapshot.paramMap.get('id') ?? '');
  readonly pack = signal<PackView | null>(null);
  readonly record = signal<IndustryRecordDto | null>(null);
  readonly failed = signal(false);
  readonly busy = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly editing = signal(false);
  readonly step = signal<Step | null>(null);
  readonly stepDate = signal(toIsoDate(new Date()));
  readonly taxCodeId = signal<string | null>(null);
  readonly vendorReference = signal('');
  readonly warehouseId = signal<string | null>(null);
  readonly chosenLines = signal<ReadonlySet<string>>(new Set());
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  readonly warehouses = signal<WarehouseRow[]>([]);
  private readonly progressEdits = signal<Record<string, number>>({});

  readonly type = computed<RecordTypeView | null>(() => this.pack()?.types.find((t) => t.key === this.typeKey()) ?? null);
  readonly canManage = computed(() => this.session.hasPermission(`${this.module()}.${this.typeKey()}.manage`));
  readonly statusText = computed(() => statusLabel(this.type(), this.record()?.status ?? ''));
  readonly unbilledLines = computed(() => this.record()?.lines.filter((l) => !l.invoiceId) ?? []);
  readonly progressDirty = computed(() => Object.keys(this.progressEdits()).length > 0);

  /** Whether the record is in a status that lets it be billed, and has not been billed already. */
  private readonly billable = computed(() => {
    const r = this.record(), t = this.type();
    if (!r || !t) return false;
    const allowed = t.billableStatuses;
    return !allowed || allowed.length === 0 || allowed.includes(r.status);
  });
  readonly canInvoice = computed(() => {
    const r = this.record(), t = this.type();
    return !!r && !!t && t.canInvoice && this.billable() && !!r.partyId && (t.perLineBilling ? this.unbilledLines().length > 0 : !r.invoiceId);
  });
  readonly canBill = computed(() => {
    const r = this.record(), t = this.type();
    return !!r && !!t && t.canBill && this.billable() && !!r.partyId && !r.billId;
  });
  readonly canConsume = computed(() => {
    const r = this.record(), t = this.type();
    return !!r && !!t && t.consumesStock && !r.consumedOn && r.lines.some((l) => !!l.itemId && l.quantity > 0);
  });
  private readonly locked = computed(() => {
    const r = this.record();
    return !!r && (!!r.invoiceId || !!r.billId || !!r.consumedOn || r.lines.some((l) => !!l.invoiceId));
  });

  readonly actions = computed<ActionButton[]>(() => {
    const r = this.record(), t = this.type();
    if (!r || !t) return [];
    const manage = `${this.module()}.${t.key}.manage`;
    const steps: ActionButton[] = r.nextSteps.map((s, i) => ({
      label: s.label, icon: s.approval ? 'check' : 'arrow-right', type: i === 0 ? 'primary' : 'default', danger: /^(cancel|reject|void)/i.test(s.to) || s.to === 'cancelled' || s.to === 'rejected',
      permission: manage, handler: () => void this.takeStep(s),
    }));
    return [
      ...steps,
      { label: 'Invoice', icon: 'file-text', type: 'primary', permission: manage, visible: this.canInvoice(), handler: () => this.openStep('invoice') },
      { label: 'Raise bill', icon: 'file-add', type: 'primary', permission: manage, visible: this.canBill(), handler: () => this.openStep('bill') },
      { label: 'Issue materials', icon: 'export', permission: manage, visible: this.canConsume(), handler: () => this.openStep('consume') },
      { label: 'Edit', icon: 'edit', permission: manage, visible: !this.locked(), handler: () => this.editing.set(true) },
      { label: 'Delete', icon: 'delete', danger: true, permission: manage, visible: !this.locked() && r.status === t.statuses[0]?.key, handler: () => this.confirmDelete() },
    ];
  });

  readonly descriptions = computed<DescriptionItem[]>(() => {
    const r = this.record(), t = this.type();
    if (!r || !t) return [];
    const items: DescriptionItem[] = [
      { label: 'Number', value: r.number }, { label: t.titleLabel || 'Name', value: r.title },
      { label: 'Status', value: this.statusText() }, { label: 'Date', value: r.recordDate },
    ];
    if (t.hasDueDate) items.push({ label: 'Due date', value: r.dueDate ?? null });
    if (t.partyRole) items.push({ label: t.partyLabel || 'Party', value: r.partyName ?? null, routerLink: r.partyId ? ['/parties', r.partyId] : undefined });
    if (t.parentType) items.push({ label: t.parentLabel || 'Belongs to', value: r.parentTitle ?? null, routerLink: r.parentId ? ['/industry', this.module(), t.parentType, r.parentId] : undefined });
    for (const f of t.fields) items.push({ label: f.label, value: this.fieldText(f.type, r.data?.[f.key]) });
    if (t.hasAmount || t.hasLines) items.push({ label: 'Amount', value: this.money(r.amount) });
    items.push({ label: 'Legal entity', value: this.session.me()?.legalEntities.find((e) => e.id === r.legalEntityId)?.name ?? null });
    if (r.consumedOn) items.push({ label: 'Materials issued on', value: r.consumedOn });
    if (r.notes) items.push({ label: 'Notes', value: r.notes, span: 2 });
    return items;
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => {
      this.module.set(p.get('module') ?? ''); this.typeKey.set(p.get('type') ?? ''); this.id.set(p.get('id') ?? '');
      this.load();
    });
  }

  load(): void {
    this.failed.set(false);
    this.api.definitions(this.module()).subscribe({
      next: (pack) => { this.pack.set(pack); if (!pack.types.some((t) => t.key === this.typeKey())) this.failed.set(true); },
      error: () => this.failed.set(true),
    });
    this.api.get(this.module(), this.typeKey(), this.id()).subscribe({ next: (r) => this.show(r), error: () => this.failed.set(true) });
  }

  private show(r: IndustryRecordDto): void {
    this.record.set(r);
    this.progressEdits.set({});
  }

  tone(status: string): string { return toneColor(this.type()?.statuses.find((s) => s.key === status)?.tone); }
  label(status: string): string { return statusLabel(this.type(), status); }
  money(v: number): string { return displayMoney(v, this.currency.currency()); }
  dateText(v: string | null | undefined): string { return v ? displayDate(v) : '—'; }
  percent(done: number, total: number): number { return total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0; }
  private fieldText(type: string, v: unknown): string | null {
    if (v === null || v === undefined || v === '') return null;
    return type === 'bool' ? (v ? 'Yes' : 'No') : type === 'date' ? String(v) : String(v);
  }

  saved(r: IndustryRecordDto): void { this.editing.set(false); this.show(r); }

  /** A step of the record's life cycle. The server decides what is allowed and explains when it is not. */
  async takeStep(step: RecordTransitionDef): Promise<void> {
    const r = this.record();
    if (!r) return;
    let reason: string | null = null;
    if (step.reasonRequired) {
      reason = await askReason(this.modal, step.label, step.label, 'Say why. The reason is kept in the history.');
      if (reason === null) return;
    }
    this.run(this.api.transition(this.module(), this.typeKey(), r.id, step.to, reason), () => doneText('updated', this.type()?.singular ?? 'Record', r.number));
  }

  private confirmDelete(): void {
    const r = this.record();
    if (!r) return;
    this.modal.confirm({
      nzTitle: `Delete ${r.number}?`, nzContent: 'It is removed for good. A record that has moved on is cancelled instead.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.remove(this.module(), this.typeKey(), r.id).subscribe({
          next: () => { this.message.success(doneText('deleted', this.type()?.singular ?? 'Record', r.number)); resolve(); void this.router.navigate(['/industry', this.module(), this.typeKey()]); },
          error: (e) => { this.actionError.set(problemMessage(e, 'Could not delete this record.')); resolve(); },
        });
      }),
    });
  }

  openStep(step: Step): void {
    this.actionError.set(null);
    this.stepDate.set(toIsoDate(new Date()));
    this.taxCodeId.set(null);
    this.vendorReference.set('');
    this.warehouseId.set(null);
    this.chosenLines.set(new Set(this.unbilledLines().map((l) => l.id)));
    if (step !== 'consume' && this.taxCodes().length === 0 && this.session.hasPermission('finance.tax.read')) {
      this.taxApi.list({ page: 1, pageSize: 100, sort: 'code' }).pipe(catchError(() => of(null))).subscribe((p) => this.taxCodes.set((p?.items ?? []).filter((c) => c.isActive)));
    }
    if (step === 'consume' && this.warehouses().length === 0 && this.session.hasPermission('inventory.warehouses.read') && this.session.isModuleEnabled('inventory')) {
      this.warehouseApi.list({ page: 1, pageSize: 100, sort: 'code' }).pipe(catchError(() => of(null))).subscribe((p) => this.warehouses.set((p?.items ?? []).filter((w) => w.isActive)));
    }
    this.step.set(step);
  }

  stepTitle(): string { return { invoice: 'Raise an invoice', bill: 'Raise a vendor bill', consume: 'Issue materials from stock' }[this.step() ?? 'invoice']; }
  stepOk(): string { return { invoice: 'Invoice', bill: 'Raise bill', consume: 'Issue materials' }[this.step() ?? 'invoice']; }

  toggleLine(id: string, on: boolean): void {
    const next = new Set(this.chosenLines());
    if (on) next.add(id); else next.delete(id);
    this.chosenLines.set(next);
  }

  runStep(): void {
    const r = this.record(), t = this.type(), step = this.step();
    if (!r || !t || !step) return;
    const date = this.stepDate() || null;
    let call: Observable<IndustryRecordDto>;
    if (step === 'invoice') {
      if (t.perLineBilling && this.chosenLines().size === 0) { this.actionError.set('Choose at least one line to invoice.'); this.step.set(null); return; }
      call = this.api.invoice(this.module(), t.key, r.id, { date, taxCodeId: this.taxCodeId(), lineIds: t.perLineBilling ? [...this.chosenLines()] : null });
    } else if (step === 'bill') {
      call = this.api.bill(this.module(), t.key, r.id, { date, taxCodeId: this.taxCodeId(), vendorReference: this.vendorReference().trim() || null });
    } else {
      call = this.api.consume(this.module(), t.key, r.id, { date, warehouseId: this.warehouseId() });
    }
    this.run(call, () => doneText(step === 'consume' ? 'issued' : 'created', step === 'invoice' ? 'Invoice' : step === 'bill' ? 'Bill' : 'Materials', r.number), () => this.step.set(null));
  }

  progressOf(lineId: string, saved: number): number { return this.progressEdits()[lineId] ?? saved; }
  setProgress(lineId: string, value: number | null): void {
    const line = this.record()?.lines.find((l) => l.id === lineId);
    if (!line || value === null || value === undefined) return;
    const next = { ...this.progressEdits() };
    if (value === line.completedQuantity) delete next[lineId]; else next[lineId] = value;
    this.progressEdits.set(next);
  }
  saveProgress(): void {
    const r = this.record();
    if (!r) return;
    const lines = Object.entries(this.progressEdits()).map(([lineId, completedQuantity]) => ({ lineId, completedQuantity }));
    this.run(this.api.progress(this.module(), this.typeKey(), r.id, lines), () => doneText('updated', 'Progress', r.number));
  }

  /** Runs a call on the record; the page shows the result, or the server's own words when it refuses (a 409 says why). */
  private run(call: Observable<IndustryRecordDto>, done: () => string, after?: () => void): void {
    this.busy.set(true);
    this.actionError.set(null);
    call.subscribe({
      next: (r) => { this.busy.set(false); this.show(r); this.message.success(done()); after?.(); },
      error: (e) => {
        this.busy.set(false);
        after?.();
        const p = asProblem(e);
        this.actionError.set(problemMessage(e, p?.status === 409 ? 'This step is not allowed right now.' : 'Could not complete this step.'));
      },
    });
  }
}

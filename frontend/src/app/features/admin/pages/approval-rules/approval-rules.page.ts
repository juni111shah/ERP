import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ApprovalPoliciesApi, ApprovalPolicy, RolesApi } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';

export const DEFAULT_APPROVER = 'core.approvals.decide';
export const APPROVAL_DOCUMENT_TYPES = [
  { value: 'bill', label: 'Vendor bill' }, { value: 'journal', label: 'Manual journal' }, { value: 'payment', label: 'Payment' },
  { value: 'purchase_order', label: 'Purchase order' }, { value: 'purchase_requisition', label: 'Purchase requisition' },
];

/** Approval rules: documents of a type at or above an amount need another person's approval before they post. */
@Component({
  selector: 'erp-approval-rules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, ErpLabelPipe, NzModalModule, TableToolsComponent, TranslatePipe, DecimalPipe, NzToolTipModule, RevealDirective, FormsModule, HasPermissionDirective, NzButtonModule, NzIconModule, NzInputNumberModule, NzSelectModule, NzSwitchModule, NzTableModule],
  templateUrl: './approval-rules.page.html',
})
export class ApprovalRulesPage {
  private readonly api = inject(ApprovalPoliciesApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly rules = signal<ApprovalPolicy[]>([]);
  readonly active = signal<'active' | 'inactive' | null>(null);
  /** Client-side filter: GET /approval-policies takes no query parameters. */
  readonly shown = computed(() => this.rules().filter((r) => !this.active() || r.isActive === (this.active() === 'active')));
  canWrite = true;
  readonly documentTypes = APPROVAL_DOCUMENT_TYPES;
  readonly approverPermissions = signal<string[]>([DEFAULT_APPROVER]);
  documentType = 'bill';
  minAmount: number | null = null;
  level = 1;
  approverPermission = DEFAULT_APPROVER;

  constructor() {
    this.load();
    // The catalogue needs the roles permission; without it the default approver permission is all that is offered.
    inject(RolesApi).permissions().subscribe({
      next: (all) => this.approverPermissions.set([...new Set([DEFAULT_APPROVER, ...all.map((p) => p.key).filter((k) => /\.(approve|decide)$/.test(k))])].sort()),
      error: () => undefined,
    });
  }

  typeLabel(type: string): string { return this.documentTypes.find((t) => t.value === type)?.label ?? displayLabel(type); }

  load(): void { this.api.list().subscribe({ next: (r) => this.rules.set(r), error: (e) => { this.message.error(problemMessage(e, 'Could not load the rules')); } }); }

  add(): void {
    this.api.save(null, { documentType: this.documentType, minAmount: this.minAmount, isActive: true, level: this.level, approverPermission: this.approverPermission }).subscribe({ next: () => { this.minAmount = null; this.load(); }, error: (e) => this.fail(e) });
  }

  toggle(r: ApprovalPolicy, on: boolean): void {
    this.api.save(r.id, { documentType: r.documentType, minAmount: r.minAmount ?? null, isActive: on, level: r.level, approverPermission: r.approverPermission }).subscribe({ next: () => this.load(), error: (e) => this.fail(e) });
  }

  remove(r: ApprovalPolicy): void {
    this.modal.confirm({
      nzTitle: 'Delete this rule?', nzContent: 'Documents it would catch will post without a second person.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.remove(r.id).subscribe({ next: () => this.load(), error: (e) => this.fail(e) }),
    });
  }

  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the rule')); }
}

import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ApprovalPoliciesApi, ApprovalPolicy, RolesApi } from '../../core/api/admin.api';
import { displayLabel } from '../../core/format/display';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TableToolsComponent } from '../../shared/table-tools.component';
import { RowActionsComponent } from '../../shared/row-actions.component';

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
  template: `
    <h1 class="erp-page-title">Approval rules</h1>
    <p>A document that matches a rule waits for a second person (with the approve permission) before it is posted. Rules of one document type on different levels form a chain: level 1 decides first, then level 2, and so on. Rules on the same level are amount tiers, and the highest amount reached applies.</p>
    <erp-table-tools [searchable]="false" (refresh)="load()" testId="rules">
      <nz-select [ngModel]="active()" (ngModelChange)="active.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:170px" data-testid="rules-status-filter"><nz-option nzValue="active" [nzLabel]="'ui.common.active' | translate: 'Active'"></nz-option><nz-option nzValue="inactive" [nzLabel]="'ui.common.inactive' | translate: 'Inactive'"></nz-option></nz-select>
    </erp-table-tools>
    <div class="erp-page-card" style="background:var(--erp-color-bg-surface); border:1px solid var(--erp-color-border-subtle); border-radius:12px; overflow:hidden; margin-bottom:16px;">
      <nz-table [nzData]="shown()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" nzNoResult="No approval rules yet, so documents post without a second person." data-testid="rules-table">
        <thead><tr><th>Document</th><th>Level</th><th>Approver permission</th><th>From amount</th><th>Active</th><th></th></tr></thead>
        <tbody>
          @for (r of shown(); track r.id) {
            <tr>
              <td><span style="font-weight:600;">{{ typeLabel(r.documentType) }}</span></td>
              <td data-testid="rule-level">{{ r.level }}</td><td><code>{{ r.approverPermission }}</code></td>
              <td><strong>{{ r.minAmount != null ? (r.minAmount | number:'1.2-2') : 'Any amount' }}</strong></td>
              <td><nz-switch [ngModel]="r.isActive" (ngModelChange)="toggle(r, $event)" [disabled]="!canWrite" aria-label="Rule is active" /></td>
              <td class="erp-cell-actions"><erp-row-actions><button *erpHasPermission="'core.settings.write'" nz-button nzDanger nzSize="small" type="button" (click)="remove(r)" data-testid="rule-delete">Delete</button></erp-row-actions></td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>
    <div *erpHasPermission="'core.settings.write'" style="display:flex;gap:12px;margin-top:16px;align-items:flex-end;flex-wrap:wrap;background:var(--erp-color-bg-surface);padding:12px 16px;border-radius:10px;border:1px solid var(--erp-color-border-subtle);">
      <div style="display:flex;flex-direction:column;gap:4px;font-size:12px;color:var(--erp-color-text-muted)">Document <nz-select [(ngModel)]="documentType" style="width:200px" data-testid="rule-type">@for (t of documentTypes; track t.value) { <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option> }</nz-select></div>
      <div style="display:flex;flex-direction:column;gap:4px;font-size:12px;color:var(--erp-color-text-muted)">Level <nz-input-number [(ngModel)]="level" [nzMin]="1" [nzMax]="10" [nzPrecision]="0" style="width:90px" data-testid="rule-level-input"></nz-input-number></div>
      <div style="display:flex;flex-direction:column;gap:4px;font-size:12px;color:var(--erp-color-text-muted)">Approver permission <nz-select [(ngModel)]="approverPermission" nzShowSearch style="width:260px" data-testid="rule-permission">@for (p of approverPermissions(); track p) { <nz-option [nzValue]="p" [nzLabel]="p"></nz-option> }</nz-select></div>
      <div style="display:flex;flex-direction:column;gap:4px;font-size:12px;color:var(--erp-color-text-muted)">From amount (empty means any) <nz-input-number [(ngModel)]="minAmount" [nzMin]="0" [nzPrecision]="2" style="width:200px" data-testid="rule-amount"></nz-input-number></div>
      <button nz-button nzType="primary" (click)="add()" data-testid="rule-add"><span nz-icon nzType="plus"></span> Add rule</button>
    </div>`,
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

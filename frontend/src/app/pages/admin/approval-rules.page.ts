import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ApprovalPoliciesApi, ApprovalPolicy } from '../../core/api/admin.api';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TableToolsComponent } from '../../shared/table-tools.component';

/** Approval rules: documents of a type at or above an amount need another person's approval before they post. */
@Component({
  selector: 'erp-approval-rules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TableToolsComponent, TranslatePipe, DecimalPipe, NzToolTipModule, RevealDirective, FormsModule, HasPermissionDirective, NzButtonModule, NzIconModule, NzInputNumberModule, NzSelectModule, NzSwitchModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">Approval rules</h1>
    <p>A document that matches a rule waits for a second person (with the approve permission) before it is posted.</p>
    <erp-table-tools [searchable]="false" (refresh)="load()" testId="rules">
      <nz-select [ngModel]="active()" (ngModelChange)="active.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:170px" data-testid="rules-status-filter"><nz-option nzValue="active" [nzLabel]="'ui.common.active' | translate: 'Active'"></nz-option><nz-option nzValue="inactive" [nzLabel]="'ui.common.inactive' | translate: 'Inactive'"></nz-option></nz-select>
    </erp-table-tools>
    <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04); margin-bottom:16px;">
      <nz-table [nzData]="shown()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="rules-table">
        <thead><tr><th>Document</th><th>From amount</th><th>Status</th><th>Actions</th></tr></thead>
        <tbody>
          @for (r of shown(); track r.id) {
            <tr>
              <td><span style="font-weight:600; color:#1e293b;">{{ r.documentType === 'bill' ? 'Vendor bill' : r.documentType }}</span></td>
              <td><strong>{{ r.minAmount != null ? (r.minAmount | number:'1.2-2') : 'Any amount' }}</strong></td>
              <td><nz-switch [ngModel]="r.isActive" (ngModelChange)="toggle(r, $event)" [disabled]="!canWrite" /></td>
              <td><a *erpHasPermission="'core.settings.write'" (click)="remove(r)" data-testid="rule-delete" style="color:#ef4444;">Delete</a></td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>
    <div *erpHasPermission="'core.settings.write'" style="display:flex;gap:12px;margin-top:16px;align-items:center;background:#f8fafc;padding:12px 16px;border-radius:10px;border:1px solid #e2e8f0;">
      <nz-select [(ngModel)]="documentType" style="width:200px" data-testid="rule-type"><nz-option nzValue="bill" nzLabel="Vendor bill"></nz-option></nz-select>
      <nz-input-number [(ngModel)]="minAmount" [nzMin]="0" [nzPrecision]="2" nzPlaceHolder="From amount" data-testid="rule-amount"></nz-input-number>
      <button nz-button nzType="primary" (click)="add()" data-testid="rule-add"><span nz-icon nzType="plus"></span> Add rule</button>
    </div>`,
})
export class ApprovalRulesPage {
  private readonly api = inject(ApprovalPoliciesApi);
  private readonly message = inject(NzMessageService);
  readonly rules = signal<ApprovalPolicy[]>([]);
  readonly active = signal<'active' | 'inactive' | null>(null);
  /** Client-side filter: GET /approval-policies takes no query parameters. */
  readonly shown = computed(() => this.rules().filter((r) => !this.active() || r.isActive === (this.active() === 'active')));
  canWrite = true;
  documentType = 'bill';
  minAmount: number | null = null;

  constructor() { this.load(); }

  load(): void { this.api.list().subscribe((r) => this.rules.set(r)); }

  add(): void {
    this.api.save(null, { documentType: this.documentType, minAmount: this.minAmount, isActive: true }).subscribe({ next: () => { this.minAmount = null; this.load(); }, error: (e) => this.fail(e) });
  }

  toggle(r: ApprovalPolicy, on: boolean): void {
    this.api.save(r.id, { documentType: r.documentType, minAmount: r.minAmount ?? null, isActive: on }).subscribe({ next: () => this.load(), error: (e) => this.fail(e) });
  }

  remove(r: ApprovalPolicy): void { this.api.remove(r.id).subscribe({ next: () => this.load(), error: (e) => this.fail(e) }); }

  private fail(e: unknown): void { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the rule'); }
}

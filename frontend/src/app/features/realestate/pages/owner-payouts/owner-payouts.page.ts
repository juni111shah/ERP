import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { AccountsApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ActionFormState } from '@shared/utils/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { askReason } from '@shared/utils/reason-prompt';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { OwnerPayoutDto, OwnerPayoutsApi, RealEstateLookups } from '@core/api/realestate.api';

/** Owner payouts: paying the owner of a managed property what was collected for the period, less the management fee and maintenance recharged. */
@Component({
  selector: 'erp-owner-payouts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzModalModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  templateUrl: './owner-payouts.page.html',
})
export class OwnerPayoutsPage {
  private readonly api = inject(OwnerPayoutsApi);
  private readonly lookups = inject(RealEstateLookups);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly accountPage = signal<{ value: string; label: string }[]>([]);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });

  readonly columns: TableColumn<OwnerPayoutDto>[] = [
    { key: 'number', label: 'Number', filter: false },
    { key: 'date', label: 'Paid on', sortField: 'date', filter: false, value: (p) => displayDate(p.date) },
    { key: 'period', label: 'Period', filter: false, value: (p) => `${displayDate(p.periodFrom)} to ${displayDate(p.periodTo)}` },
    { key: 'rentCollected', label: 'Rent collected', align: 'right', filter: false, value: (p) => p.rentCollected.toFixed(2) },
    { key: 'managementFee', label: 'Management fee', align: 'right', filter: false, value: (p) => p.managementFee.toFixed(2) },
    { key: 'maintenanceCosts', label: 'Maintenance', align: 'right', filter: false, value: (p) => p.maintenanceCosts.toFixed(2) },
    { key: 'amount', label: 'Paid to owner', align: 'right', filter: false },
    { key: 'method', label: 'How', filter: false, value: (p) => (p.method === 'bill' ? 'Bill' : 'Bank payment') },
    { key: 'bill', label: 'Bill', filter: false, value: (p) => (p.billId ? 'Bill' : '') },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['posted', 'voided'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: legalEntityOptions(this.session), default: defaultLegalEntityId(this.session) ?? legalEntityOptions(this.session)[0]?.value ?? null },
    { key: 'ownerPartyId', label: 'Owner', type: 'lookup', required: true, lookup: this.lookups.party('other') },
    { key: 'from', label: 'Period from', type: 'date', required: true },
    { key: 'to', label: 'Period to', type: 'date', required: true, hint: 'A period that overlaps an earlier payout is refused. A shortfall carries on: choose a longer period.' },
    { key: 'date', label: 'Payout date', type: 'date', required: true },
    { key: 'method', label: 'Pay by', type: 'select', required: true, default: 'payment', options: [{ value: 'payment', label: 'Bank payment' }, { value: 'bill', label: 'Bill to the owner' }] },
    { key: 'bankAccountId', label: 'Pay from', type: 'select', options: this.accountPage(), hidden: (v) => v['method'] === 'bill', hint: 'The bank or cash account the money leaves.' },
    { key: 'dueDate', label: 'Bill due date', type: 'date', hidden: (v) => v['method'] !== 'bill' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ]);

  readonly pay = new ActionFormState({
    run: (p) => this.api.create({ ...p, bankAccountId: p['method'] === 'bill' ? null : p['bankAccountId'] ?? null, dueDate: p['method'] === 'bill' ? p['dueDate'] ?? null : null }),
    success: (r) => doneText('posted', 'Owner payout', (r as OwnerPayoutDto).number), done: () => this.list.reload(), message: this.message, failure: 'Could not pay the owner',
  });

  constructor() {
    inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({
      next: (r) => this.accountPage.set(r.items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank' || a.systemRole === 'cash')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` }))),
      error: () => undefined,
    });
  }

  voidIt(row: OwnerPayoutDto): void {
    askReason(this.modal, 'Void this payout?', 'Void payout', 'The entry is reversed and, for a bill, the bill is voided too (refused once it has been paid). The period can then be paid again.').then((reason) => {
      if (!reason) return;
      this.api.void(row.id, reason).subscribe({
        next: () => { this.message.success(doneText('voided', 'Owner payout', row.number)); this.list.reload(); },
        error: (e) => this.message.error(problemMessage(e, 'Could not void the payout', 'reason')),
      });
    });
  }
}

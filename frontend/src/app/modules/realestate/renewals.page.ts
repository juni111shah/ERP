import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { RenewalWorkbench, RenewalsApi } from './realestate.api';

/** Leases ending soon: see who is due, compare rent with the unit's asking rent and create draft renewals in bulk with a rent change. */
@Component({
  selector: 'erp-renewals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterLink, NzTableModule, NzButtonModule, NzCheckboxModule, NzInputNumberModule, NzSelectModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Lease renewals</h1>
    <div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:16px">
      <span>Ending within</span>
      <nz-select style="width:120px" [ngModel]="days()" (ngModelChange)="days.set($event); load()" data-testid="renewal-window">
        @for (d of [30, 60, 90, 120, 180]; track d) { <nz-option [nzValue]="d" [nzLabel]="d + ' days'"></nz-option> }
      </nz-select>
      <span style="flex:1"></span>
      <span>Rent change %</span>
      <nz-input-number [ngModel]="uplift()" (ngModelChange)="uplift.set($event ?? 0)" [nzMin]="-50" [nzMax]="100" data-testid="renewal-uplift"></nz-input-number>
      <button nz-button nzType="primary" [disabled]="selected().size === 0" [nzLoading]="busy()" (click)="createDrafts()" data-testid="renewal-create">Create draft renewals ({{ selected().size }})</button>
    </div>
    @if (data(); as d) {
      <p>{{ d.due }} due · {{ d.draft }} draft renewal(s) · {{ d.renewed }} renewed</p>
      <nz-table [nzData]="d.rows" nzSize="small" [nzShowPagination]="false" data-testid="renewal-table">
        <thead><tr><th></th><th>Lease</th><th>Tenant</th><th>Property · Unit</th><th>Ends</th><th>Days</th><th>Rent</th><th>vs asking</th><th>State</th></tr></thead>
        <tbody>
          @for (r of d.rows; track r.leaseId) {
            <tr>
              <td><label nz-checkbox [ngModel]="selected().has(r.leaseId)" [disabled]="r.state !== 'due'" (ngModelChange)="toggle(r.leaseId, $event)"></label></td>
              <td><a [routerLink]="['/real-estate/leases', r.leaseId]">{{ r.number }}</a></td>
              <td>{{ r.tenant }}</td><td>{{ r.property }} · {{ r.unit }}</td><td>{{ r.endDate }}</td><td>{{ r.daysLeft }}</td>
              <td>{{ r.annualRent | number: '1.0-0' }} {{ r.currencyCode }}</td>
              <td>{{ r.marketVariancePercent === null || r.marketVariancePercent === undefined ? '—' : (r.marketVariancePercent > 0 ? '+' : '') + r.marketVariancePercent + '%' }}</td>
              <td><nz-tag [nzColor]="r.state === 'renewed' ? 'green' : r.state === 'draftRenewal' ? 'blue' : 'orange'">{{ r.state === 'due' ? 'Due' : r.state === 'draftRenewal' ? 'Draft renewal' : 'Renewed' }}</nz-tag>
                @if (r.renewalLeaseId) { <a [routerLink]="['/real-estate/leases', r.renewalLeaseId]">open</a> }</td>
            </tr>
          }
        </tbody>
      </nz-table>
    }`,
})
export class RenewalsPage {
  private readonly api = inject(RenewalsApi);
  private readonly message = inject(NzMessageService);
  readonly days = signal(120);
  readonly uplift = signal(0);
  readonly data = signal<RenewalWorkbench | null>(null);
  readonly selected = signal<Set<string>>(new Set());
  readonly busy = signal(false);

  constructor() { this.load(); }

  load(): void { this.selected.set(new Set()); this.api.workbench(this.days()).subscribe((d) => this.data.set(d)); }

  toggle(id: string, on: boolean): void { this.selected.update((s) => { const n = new Set(s); if (on) n.add(id); else n.delete(id); return n; }); }

  createDrafts(): void {
    this.busy.set(true);
    this.api.createDrafts([...this.selected()], this.uplift()).subscribe({
      next: (results) => {
        this.busy.set(false);
        const failed = results.filter((r) => r.error);
        this.message[failed.length ? 'warning' : 'success'](`${results.length - failed.length} draft renewal(s) created${failed.length ? `, ${failed.length} skipped: ${failed[0].error}` : ''}`);
        this.load();
      },
      error: () => { this.busy.set(false); this.message.error('Could not create the renewals'); },
    });
  }
}

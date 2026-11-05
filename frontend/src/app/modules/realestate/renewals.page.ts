import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { I18nService } from '../../core/i18n/i18n.service';
import { ErpDatePipe } from '../../shared/display.pipes';

/** Leases ending soon: see who is due, compare rent with the unit's asking rent and create draft renewals in bulk with a rent change. */
@Component({
  selector: 'erp-renewals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, StatePanelComponent, TableToolsComponent, TranslatePipe, DecimalPipe, FormsModule, RouterLink, NzTableModule, NzButtonModule, NzCheckboxModule, NzInputNumberModule, NzSelectModule, NzTagModule],
  styles: [`.num { text-align: end; white-space: nowrap; } .sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; } .open-link { margin-inline-start: 6px; }`],
  template: `
    <h1 class="erp-page-title">Lease renewals</h1>
    @if (data(); as d) {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="renewal-kpis">
        <erp-stat-card [label]="'ui.re.dueLc' | translate: 'Due'" [value]="d.due" format="number" caption="no renewal started" [tone]="d.due > 0 ? 'warning' : 'neutral'" />
        <erp-stat-card [label]="'ui.re.draftRenewals' | translate: 'Draft renewals'" [value]="d.draft" format="number" caption="waiting to be activated" [tone]="2" />
        <erp-stat-card [label]="'ui.re.renewedLc' | translate: 'Renewed'" [value]="d.renewed" format="number" [tone]="4" />
      </div>
    }
    <div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:16px">
      <label for="renewal-window">Ending within</label>
      <nz-select nzId="renewal-window" style="width:120px" [ngModel]="days()" (ngModelChange)="days.set($event); load()" data-testid="renewal-window">
        @for (d of [30, 60, 90, 120, 180]; track d) { <nz-option [nzValue]="d" [nzLabel]="d + ' days'"></nz-option> }
      </nz-select>
      <span style="flex:1"></span>
      <label for="renewal-uplift">Rent change %</label>
      <nz-input-number nzId="renewal-uplift" [ngModel]="uplift()" (ngModelChange)="uplift.set($event ?? 0)" [nzMin]="-50" [nzMax]="100" data-testid="renewal-uplift"></nz-input-number>
      <button nz-button nzType="primary" [disabled]="selected().size === 0" [nzLoading]="busy()" (click)="createDrafts()" data-testid="renewal-create">{{ 'ui.re.createDraftRenewals' | translate: 'Create draft renewals' }} ({{ selected().size }})</button>
    </div>
    <erp-table-tools [(search)]="q" (refresh)="load()" testId="renewal">
      <nz-select [ngModel]="state()" (ngModelChange)="state.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:170px" data-testid="renewal-state-filter"><nz-option nzValue="due" [nzLabel]="'ui.status.due' | translate: 'Due'"></nz-option><nz-option nzValue="draftRenewal" [nzLabel]="'ui.status.draftRenewal' | translate: 'Draft renewal'"></nz-option><nz-option nzValue="renewed" [nzLabel]="'ui.status.renewed' | translate: 'Renewed'"></nz-option></nz-select>
    </erp-table-tools>
    @if (failed()) {
      <erp-state-panel kind="error" title="Renewals could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else if (data(); as d) {
      @if (d.rows.length === 0) {
        <erp-state-panel [title]="'No leases end within ' + days() + ' days'" icon="check-circle"
                         [hint]="days() < 180 ? 'There is nothing to renew yet. Look further ahead to plan early.' : 'There is nothing to renew yet. Leases appear here as their end date gets closer.'"
                         [actionLabel]="days() < 180 ? 'Look 180 days ahead' : null" (action)="days.set(180); load()" data-testid="renewal-empty" />
      } @else {
        <nz-table [nzData]="shown()" nzSize="small" [nzFrontPagination]="false" [nzShowPagination]="false" nzNoResult="No leases match this search or filter" data-testid="renewal-table">
          <thead><tr>
            <th><label nz-checkbox [nzDisabled]="!dueShown().length" [ngModel]="allDueSelected()" (ngModelChange)="toggleAll($event)" data-testid="renewal-select-all"><span class="sr">Select all due leases</span></label></th>
            <th>Lease</th><th>Tenant</th><th>Property · Unit</th><th>Ends</th><th class="num">Days left</th><th class="num">Annual rent</th><th class="num">Against asking rent</th><th>State</th></tr></thead>
          <tbody>
            @for (r of shown(); track r.leaseId) {
              <tr>
                <td><label nz-checkbox [ngModel]="selected().has(r.leaseId)" [nzDisabled]="r.state !== 'due'" (ngModelChange)="toggle(r.leaseId, $event)"><span class="sr">Select {{ r.number }}</span></label></td>
                <td style="white-space:nowrap"><a [routerLink]="['/real-estate/leases', r.leaseId]">{{ r.number }}</a></td>
                <td>{{ r.tenant }}</td><td>{{ r.property }} · {{ r.unit }}</td><td style="white-space:nowrap">{{ r.endDate | erpDate }}</td><td class="num">{{ r.daysLeft }}</td>
                <td class="num">{{ r.currencyCode }} {{ r.annualRent | number: '1.0-0' }}</td>
                <td class="num">{{ r.marketVariancePercent === null || r.marketVariancePercent === undefined ? '—' : (r.marketVariancePercent > 0 ? '+' : '') + r.marketVariancePercent + '%' }}</td>
                <td style="white-space:nowrap"><nz-tag [nzColor]="r.state === 'renewed' ? 'green' : r.state === 'draftRenewal' ? 'blue' : 'orange'">{{ r.state === 'due' ? 'Due' : r.state === 'draftRenewal' ? 'Draft renewal' : 'Renewed' }}</nz-tag>
                  @if (r.renewalLeaseId) { <a class="open-link" [routerLink]="['/real-estate/leases', r.renewalLeaseId]">Open renewal</a> }</td>
              </tr>
            }
          </tbody>
        </nz-table>
      }
    }`,
})
export class RenewalsPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(RenewalsApi);
  private readonly message = inject(NzMessageService);
  readonly days = signal(120);
  readonly uplift = signal(0);
  readonly data = signal<RenewalWorkbench | null>(null);
  readonly q = signal('');
  readonly state = signal<string | null>(null);
  /** Client-side filter: GET /realestate/renewals only takes the `days` window (the select above). */
  readonly shown = computed(() => (this.data()?.rows ?? []).filter((r) => matchText(this.q(), r.number, r.tenant, r.property, r.unit) && (!this.state() || r.state === this.state())));
  readonly selected = signal<Set<string>>(new Set());
  readonly busy = signal(false);
  readonly failed = signal(false);
  readonly dueShown = computed(() => this.shown().filter((r) => r.state === 'due'));
  readonly allDueSelected = computed(() => this.dueShown().length > 0 && this.dueShown().every((r) => this.selected().has(r.leaseId)));

  constructor() { this.load(); }

  load(): void {
    this.selected.set(new Set()); this.failed.set(false);
    this.api.workbench(this.days()).subscribe({ next: (d) => this.data.set(d), error: () => this.failed.set(true) });
  }

  toggleAll(on: boolean): void { this.selected.set(new Set(on ? this.dueShown().map((r) => r.leaseId) : [])); }

  toggle(id: string, on: boolean): void { this.selected.update((s) => { const n = new Set(s); if (on) n.add(id); else n.delete(id); return n; }); }

  createDrafts(): void {
    this.busy.set(true);
    this.api.createDrafts([...this.selected()], this.uplift()).subscribe({
      next: (results) => {
        this.busy.set(false);
        const failed = results.filter((r) => r.error);
        this.message[failed.length ? 'warning' : 'success'](`${results.length - failed.length} ${this.i18n.t('ui.msg.draftRenewalsCreated', 'draft renewal(s) created')}${failed.length ? `, ${failed.length} ${this.i18n.t('ui.msg.skipped', 'skipped')}: ${failed[0].error}` : ''}`);
        this.load();
      },
      error: () => { this.busy.set(false); this.message.error('Could not create the renewals'); },
    });
  }
}

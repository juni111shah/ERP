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
import { RenewalWorkbench, RenewalsApi } from '@core/api/realestate.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { I18nService } from '@core/i18n/i18n.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Leases ending soon: see who is due, compare rent with the unit's asking rent and create draft renewals in bulk with a rent change. */
@Component({
  selector: 'erp-renewals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, StatePanelComponent, TableToolsComponent, TranslatePipe, DecimalPipe, FormsModule, RouterLink, NzTableModule, NzButtonModule, NzCheckboxModule, NzInputNumberModule, NzSelectModule, NzTagModule],
  styleUrl: './renewals.page.scss',
  templateUrl: './renewals.page.html',
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

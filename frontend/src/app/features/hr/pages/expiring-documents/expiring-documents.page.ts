import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { DOCUMENT_TYPES, EmployeeDocumentsApi, ExpiringDocumentDto } from '@core/api/hr-people.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Passports, visas, residence IDs, labour cards and other documents that are expired or about to expire. */
@Component({
  selector: 'erp-expiring-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, NzSelectModule, NzTableModule, StatePanelComponent, StatusTagComponent],
  templateUrl: './expiring-documents.page.html',
})
export class ExpiringDocumentsPage {
  private readonly api = inject(EmployeeDocumentsApi);
  readonly dayOptions = [30, 60, 90, 180, 365];
  readonly days = signal(60);
  readonly rows = signal<ExpiringDocumentDto[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  constructor() { this.load(); }
  setDays(d: number): void { this.days.set(d); this.load(); }
  typeLabel(t: string): string { return DOCUMENT_TYPES.find((x) => x.value === t)?.label ?? t; }
  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.api.expiring(this.days()).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
  }
}

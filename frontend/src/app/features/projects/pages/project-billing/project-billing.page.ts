import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ProjectBillingApi, ProjectBillingSummaryDto } from '@core/api/projects.api';
import { CsvExportService } from '@core/services/csv-export.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { BillModalComponent } from '../../components/bill-modal/bill-modal.component';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { errorText, PRJ_STYLES } from '../../projects-ui';

/** What is ready to bill across all projects. Billing a project opens the invoice builder, where nothing is created until it is confirmed. */
@Component({
  selector: 'erp-project-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, NzCardModule, NzButtonModule, NzIconModule, NzTableModule, HasPermissionDirective, StatCardComponent, BillModalComponent, ProjectTagComponent],
  templateUrl: './project-billing.page.html',
  styles: [PRJ_STYLES],
})
export class ProjectBillingPage implements OnInit {
  private readonly billingApi = inject(ProjectBillingApi);
  private readonly msg = inject(NzMessageService);
  private readonly csv = inject(CsvExportService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly bill = viewChild(BillModalComponent);
  readonly items = signal<ProjectBillingSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly total = computed(() => this.items().reduce((a, x) => a + x.totalUnbilledAmount, 0));
  readonly billableCount = computed(() => this.items().filter((x) => x.totalUnbilledAmount > 0).length);

  ngOnInit(): void { this.loadOverview(); }

  sum(key: 'unbilledHours' | 'unbilledHoursAmount' | 'achievedMilestonesCount' | 'achievedMilestonesAmount' | 'unbilledExpensesAmount' | 'awaitingApprovalAmount'): number {
    return this.items().reduce((a, x) => a + (x[key] ?? 0), 0);
  }

  loadOverview(): void {
    this.loading.set(true);
    this.billingApi.getOverview().subscribe({
      next: (data) => { this.items.set(data); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.msg.error(errorText(e, 'Could not load the billing overview.')); },
    });
  }

  export(): void {
    this.csv.exportToCsv(this.items() as unknown as Record<string, unknown>[], 'project-billing', [
      { field: 'projectNumber', header: 'Project' }, { field: 'projectName', header: 'Name' }, { field: 'billingType', header: 'Billing' }, { field: 'unbilledHours', header: 'Hours' }, { field: 'unbilledHoursAmount', header: 'Time' },
      { field: 'achievedMilestonesAmount', header: 'Milestones' }, { field: 'unbilledExpensesAmount', header: 'Expenses' }, { field: 'awaitingApprovalAmount', header: 'Awaiting approval' }, { field: 'fixedFeeRemaining', header: 'Fee left' }, { field: 'totalUnbilledAmount', header: 'Ready to bill' },
    ]);
  }
}

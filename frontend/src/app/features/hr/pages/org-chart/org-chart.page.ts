import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { OrgChartApi, OrgChartDto } from '@core/api/hr-people.api';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** The organisation: departments with their people, and the reporting lines (who reports to whom). */
@Component({
  selector: 'erp-org-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, NzSkeletonModule, NzTabsModule, StatePanelComponent],
  styleUrl: './org-chart.page.scss',
  templateUrl: './org-chart.page.html',
})
export class OrgChartPage {
  private readonly orgChartApi = inject(OrgChartApi);
  readonly chart = signal<OrgChartDto | null>(null);
  readonly failed = signal(false);

  constructor() { this.load(); }

  load(): void { this.failed.set(false); this.orgChartApi.get().subscribe({ next: (c) => this.chart.set(c), error: () => this.failed.set(true) }); }
  /** "Sara, Omar, Lina": the members of a department. */
  names(list: { name: string }[]): string { return list.map((m) => m.name).join(', '); }
}

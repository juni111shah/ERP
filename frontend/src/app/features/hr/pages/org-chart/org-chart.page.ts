import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { OrgChartApi, OrgChartDto } from '@core/api/hr-people.api';

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
  private readonly api = inject(OrgChartApi);
  readonly chart = signal<OrgChartDto | null>(null);
  readonly failed = signal(false);
  constructor() { this.load(); }
  load(): void { this.failed.set(false); this.api.get().subscribe({ next: (c) => this.chart.set(c), error: () => this.failed.set(true) }); }
  names(list: { name: string }[]): string { return list.map((m) => m.name).join(', '); }
}

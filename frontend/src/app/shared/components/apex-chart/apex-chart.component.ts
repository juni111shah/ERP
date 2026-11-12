import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, computed, effect, input, viewChild } from '@angular/core';
import ApexCharts from 'apexcharts';
import { DashboardChart } from '@core/api/admin.api';
import { buildChartOptions } from './apex-chart-options';

/**
 * Modern, smooth, interactive ApexCharts component for the ERP dashboard.
 * Supports standard column, stacked bar, and horizontal bar charts with smooth animations,
 * custom brand palette, and refined tooltip formatting.
 */
@Component({
  selector: 'erp-apex-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './apex-chart.component.scss',
  templateUrl: './apex-chart.component.html',
})
export class ApexChartComponent implements AfterViewInit, OnDestroy {
  readonly chart = input.required<DashboardChart>();
  private readonly chartContainer = viewChild<ElementRef<HTMLDivElement>>('chartRef');

  private apexInstance: ApexCharts | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private readonly isBrowser = typeof window !== 'undefined';

  readonly hasData = computed(() => this.chart().series.some((s) => s.values.some((v) => v !== 0)));

  constructor() {
    // Re-render or update whenever the chart input changes.
    effect(() => {
      const current = this.chart();
      const has = this.hasData();
      if (this.isBrowser && this.apexInstance && has) {
        this.apexInstance.updateOptions(buildChartOptions(current), true, true);
      } else if (this.isBrowser && !this.apexInstance && has) {
        setTimeout(() => this.initChart(), 0);
      }
    });
  }

  ngAfterViewInit(): void {
    if (this.isBrowser && this.hasData()) this.initChart();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.apexInstance?.destroy();
    this.apexInstance = null;
  }

  private initChart(): void {
    const el = this.chartContainer()?.nativeElement;
    if (!el) return;

    this.apexInstance?.destroy();
    this.apexInstance = new ApexCharts(el, buildChartOptions(this.chart()));
    this.apexInstance.render();

    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver?.disconnect();
      this.resizeObserver = new ResizeObserver(() => {
        if (this.apexInstance) window.dispatchEvent(new Event('resize'));
      });
      this.resizeObserver.observe(el);
    }
  }
}

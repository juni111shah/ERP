/** The ApexCharts options of an <erp-apex-chart>: colours by series or category, bar layout per chart kind, compact number labels. */
import type { ApexOptions } from 'apexcharts';
import { DashboardChart } from '@core/api/admin.api';

const BRAND_PALETTE = ['#10955d', '#2563eb', '#f59e0b', '#8b5cf6', '#06b6d4', '#ef4444', '#14b8a6', '#6366f1'];

const SERIES_COLOR_MAP: Record<string, string> = {
  // Finance
  'billed': '#10955d',
  'collected': '#2563eb',
  'open balance': '#10955d',
  'expenses': '#0b8457',
  // Real Estate
  'occupied': '#10955d',
  'vacant': '#94a3b8',
  'reserved': '#f59e0b',
  'unavailable': '#ef4444',
  'leases ending': '#10955d',
  'units': '#10955d',
  // Sales & Commercial
  'orders': '#2563eb',
  'sales orders': '#2563eb',
  'opportunities': '#8b5cf6',
  'pipeline': '#8b5cf6',
  // Operations & Delivery
  'procurement': '#f59e0b',
  'purchase orders': '#f59e0b',
  'work orders': '#0284c7',
  'tasks': '#06b6d4',
  'employees': '#10b981',
};

const CATEGORY_COLOR_MAP: Record<string, string> = {
  // Common lifecycle & order statuses
  'draft': '#94a3b8',
  'confirmed': '#3b82f6',
  'processing': '#f59e0b',
  'delivered': '#10b981',
  'invoiced': '#059669',
  'completed': '#10b981',
  'cancelled': '#ef4444',
  'planned': '#8b5cf6',
  'in progress': '#0284c7',
  'to do': '#94a3b8',
  'in review': '#f59e0b',
  'done': '#10b981',
  'approved': '#3b82f6',
  'partially received': '#f59e0b',
  'received': '#10b981',
  // CRM Funnel stages
  'prospecting': '#64748b',
  'qualification': '#3b82f6',
  'proposal': '#8b5cf6',
  'negotiation': '#f59e0b',
  'won': '#10b981',
  'lost': '#ef4444',
  // Workforce
  'active': '#10b981',
  'on leave': '#f59e0b',
  'probation': '#8b5cf6',
  // Receivables Ageing
  'current': '#10b981',
  '1-30': '#3b82f6',
  '31-60': '#f59e0b',
  '61-90': '#f97316',
  '90+': '#ef4444',
};

/** A known name's own colour, else the brand palette by position. */
function colorOf(name: string, index: number, known: Record<string, string>): string {
  return known[name.toLowerCase().trim()] || BRAND_PALETTE[index % BRAND_PALETTE.length];
}

/** 1.2M, 35K or 812 (rounded), for axis and bar labels. */
export function formatCompact(val: number): string {
  if (val === null || val === undefined || isNaN(val)) return '0';
  const abs = Math.abs(val);
  const sign = val < 0 ? '-' : '';
  if (abs >= 1_000_000) {
    return `${sign}${(abs / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
  }
  if (abs >= 1_000) {
    return `${sign}${(abs / 1_000).toFixed(0)}K`;
  }
  return `${sign}${Math.round(abs)}`;
}

/**
 * The chart's ApexCharts options. A single series over several categories is drawn "distributed" (one colour per category) unless
 * the chart asks for a single colour; horizontal bars grow with the number of categories and carry their values as labels.
 */
export function buildChartOptions(c: DashboardChart): ApexOptions {
  const isHbar = c.kind === 'hbar';
  const isStacked = c.kind === 'stacked';
  const isSingleSeries = c.series.length === 1;
  const isDistributed = isSingleSeries && !isStacked && c.categories.length > 1 && !c.singleColor;

  const colors = isDistributed
    ? c.categories.map((cat, idx) => colorOf(cat, idx, CATEGORY_COLOR_MAP))
    : c.series.map((s, idx) => colorOf(s.name, idx, SERIES_COLOR_MAP));

  const seriesData = c.series.map((s) => ({
    name: s.name,
    data: s.values.map((v) => Math.round(v * 100) / 100),
  }));

  return {
    series: seriesData,
    chart: {
      type: 'bar',
      width: '100%',
      redrawOnWindowResize: true,
      redrawOnParentResize: true,
      height: isHbar ? Math.max(c.categories.length * 40 + 70, 270) : 310,
      stacked: isStacked,
      toolbar: {
        show: false,
      },
      fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
      animations: {
        enabled: true,
        easing: 'easeinout',
        speed: 700,
        animateGradually: {
          enabled: true,
          delay: 100,
        },
        dynamicAnimation: {
          enabled: true,
          speed: 350,
        },
      },
    },
    colors,
    plotOptions: {
      bar: {
        horizontal: isHbar,
        borderRadius: 6,
        borderRadiusApplication: 'end',
        borderRadiusWhenStacked: 'last',
        columnWidth: c.categories.length > 8 ? '52%' : '44%',
        barHeight: isHbar ? (c.categories.length > 5 ? '60%' : '50%') : undefined,
        distributed: isDistributed,
        dataLabels: {
          position: isHbar ? 'right' : 'top',
        },
      },
    },
    dataLabels: {
      enabled: isHbar,
      textAnchor: 'start',
      offsetX: 8,
      style: {
        fontSize: '11px',
        fontWeight: 600,
        colors: ['var(--erp-color-text-secondary)'],
      },
      formatter: (val: number) => formatCompact(val),
    },
    stroke: {
      show: true,
      width: isStacked ? 1 : 0,
      colors: ['var(--erp-color-bg-surface)'],
    },
    xaxis: {
      categories: c.categories,
      labels: {
        style: {
          colors: 'var(--erp-color-viz-axis)',
          fontSize: '11px',
          fontWeight: 500,
        },
        rotate: c.categories.length > 10 ? -45 : 0,
        rotateAlways: false,
        formatter: isHbar ? (val: string) => formatCompact(Number(val)) : undefined,
      },
      axisBorder: {
        show: true,
        color: 'var(--erp-color-border-default)',
      },
      axisTicks: {
        show: false,
      },
    },
    yaxis: {
      labels: {
        style: {
          colors: 'var(--erp-color-viz-axis)',
          fontSize: '11px',
          fontWeight: 500,
        },
        formatter: isHbar ? undefined : (val: number) => formatCompact(val),
      },
    },
    grid: {
      borderColor: 'var(--erp-color-viz-grid)',
      strokeDashArray: 4,
      xaxis: {
        lines: {
          show: isHbar,
        },
      },
      yaxis: {
        lines: {
          show: !isHbar,
        },
      },
      padding: {
        top: 0,
        right: 20,
        bottom: 0,
        left: 10,
      },
    },
    tooltip: {
      theme: 'light',
      shared: !isHbar,
      intersect: false,
      y: {
        formatter: (val: number) => {
          if (val === null || val === undefined) return '0';
          return new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(val);
        },
      },
    },
    legend: {
      show: !isSingleSeries && !isDistributed,
      position: 'bottom',
      horizontalAlign: 'left',
      offsetX: 6,
      offsetY: 6,
      fontSize: '12px',
      fontWeight: 500,
      markers: {
        size: 6,
        shape: 'circle',
        strokeWidth: 0,
      },
      itemMargin: {
        horizontal: 12,
        vertical: 4,
      },
    },
  };
}

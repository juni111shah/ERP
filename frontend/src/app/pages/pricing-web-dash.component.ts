import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { lastMonths } from './pricing-mobile.component';

const GROUPS = ['Sales', 'Purchases', 'Finance & Accounting', 'Master Data', 'CRM', 'Services', 'Inventory', 'Point of Sale', 'Retail', 'Distribution', 'Projects', 'Manufacturing', 'HR and Payroll', 'Setup'];

/** The web app's real Overview page, drawn at desktop size with the labels and layout it really has. Sample figures only. */
@Component({
  selector: 'erp-web-dash',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule, BrandLogoComponent],
  styles: [`
    :host { display: grid; grid-template-columns: 232px minmax(0, 1fr); width: 1180px; height: 760px; background: #f3f5f7; color: #17212b; font-size: 13px; text-align: start; overflow: hidden; }
    aside { background: #fff; border-inline-end: 1px solid #e6e9ec; padding: 14px 0; overflow: hidden; }
    .logo { padding: 0 14px 12px; }
    .menu-search { margin: 0 14px 14px; padding: 7px 10px; border: 1px solid #dfe9e4; border-radius: 8px; color: #8a949e; font-size: 12px; background: #fbfcfc; }
    .group { display: flex; justify-content: space-between; padding: 7px 18px; font-size: 11px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; color: #2d3742; }
    .group [nz-icon] { font-size: 10px; color: #6b7682; }
    .link { display: flex; align-items: center; gap: 10px; padding: 8px 24px; color: #3c4650; }
    .link.on { color: #0b7a4b; font-weight: 600; background: #e9f7f0; margin: 0 8px; padding: 8px 16px; border-radius: 8px; border: 1px solid #bfe6d3; }
    main { min-width: 0; display: flex; flex-direction: column; }
    .bar { display: flex; align-items: center; gap: 10px; height: 54px; padding: 0 22px; background: #fff; border-bottom: 1px solid #e6e9ec; }
    .crumb { padding: 2px 9px; border-radius: 7px; background: #e9f7f0; border: 1px solid #bfe6d3; color: #0b7a4b; font-size: 11px; font-weight: 700; letter-spacing: .04em; }
    .bar strong { font-size: 15px; }
    .bar .sep { color: #8a949e; }
    .tools { margin-inline-start: auto; display: flex; align-items: center; gap: 12px; color: #3c4650; }
    .tool { width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; border: 1px solid #e0e4e8; border-radius: 8px; }
    .tool.add { background: #10955d; border-color: #10955d; color: #fff; }
    .bell { position: relative; font-size: 16px; }
    .bell b { position: absolute; top: -8px; inset-inline-end: -9px; min-width: 16px; height: 16px; border-radius: 9999px; background: #e02b2b; color: #fff; font-size: 9.5px; display: flex; align-items: center; justify-content: center; }
    .avatar { width: 28px; height: 28px; border-radius: 50%; border: 1.5px solid #10955d; color: #0b7a4b; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 700; }
    .page { padding: 16px 22px; display: flex; flex-direction: column; gap: 12px; flex: 1; min-height: 0; }
    .range { display: flex; align-items: center; gap: 8px; color: #55606b; font-size: 12.5px; }
    .select { display: flex; justify-content: space-between; gap: 34px; padding: 6px 11px; background: #fff; border: 1px solid #cfd5da; border-radius: 7px; color: #17212b; }
    .tabs { display: flex; align-items: center; gap: 18px; padding: 7px 10px; background: #fff; border-radius: 12px; color: #3c4650; font-size: 15px; }
    .tabs .all { display: flex; align-items: center; gap: 7px; padding: 6px 12px; border-radius: 9px; background: #e9f7f0; border: 1px solid #bfe6d3; color: #0b7a4b; font-size: 12.5px; font-weight: 600; }
    .tabs .all b { padding: 0 6px; border-radius: 9999px; background: #cfeedd; font-size: 10.5px; }
    .switch { margin-inline-start: auto; display: flex; padding: 3px; border-radius: 8px; background: #eef1f3; font-size: 11.5px; color: #55606b; }
    .switch span { padding: 4px 10px; border-radius: 6px; }
    .switch .on { background: #fff; color: #17212b; font-weight: 600; }
    .label { font-size: 11px; font-weight: 600; letter-spacing: .06em; color: #4c5661; text-transform: uppercase; }
    .kpis { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
    .kpi { position: relative; padding: 13px 15px 12px 18px; background: #fff; border-radius: 11px; overflow: hidden; }
    .kpi::before { content: ''; position: absolute; inset-block: 0; inset-inline-start: 0; width: 4px; background: var(--c); }
    .kpi small { display: block; font-size: 10.5px; font-weight: 600; letter-spacing: .04em; color: #4c5661; text-transform: uppercase; }
    .kpi strong { display: block; font-size: 21px; font-weight: 800; letter-spacing: -.01em; color: var(--c); margin: 3px 0 2px; white-space: nowrap; }
    .kpi em { font-style: normal; font-size: 11.5px; color: #66707c; }
    .row { display: grid; grid-template-columns: minmax(0, 1.75fr) minmax(0, 1fr); gap: 12px; flex: 1; min-height: 0; }
    .card { background: #fff; border-radius: 13px; padding: 15px 18px; min-width: 0; }
    .eyebrow { display: flex; align-items: center; gap: 5px; color: #0b7a4b; font-size: 10.5px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; }
    h4 { margin: 3px 0 2px; font-size: 16px; font-weight: 700; color: #17212b; }
    .sub { margin: 0 0 9px; font-size: 11.5px; color: #55606b; }
    .pills { display: flex; gap: 6px; margin-bottom: 8px; }
    .pills span { padding: 3px 10px; border: 1px solid #e0e4e8; border-radius: 7px; font-size: 11.5px; color: #3c4650; font-weight: 500; }
    .pills .on { background: #e9f7f0; border-color: #bfe6d3; color: #0b7a4b; }
    .plot { position: relative; height: 196px; margin-inline-start: 34px; display: flex; align-items: flex-end; gap: 11px; border-bottom: 1px solid #dfe3e7; }
    .plot .tick { position: absolute; inset-inline: 0; border-top: 1px dashed #edf0f2; }
    .plot .tick span { position: absolute; inset-inline-start: -34px; top: -7px; font-size: 10px; color: #55606b; }
    .month { position: relative; flex: 1; height: 100%; display: flex; align-items: flex-end; justify-content: center; gap: 2px; }
    .month i { width: 12px; border-radius: 4px 4px 0 0; transform-origin: bottom; animation: rise .9s cubic-bezier(.2, .8, .2, 1) backwards; }
    .month i.a { background: #10955d; }
    .month i.b { background: #2563eb; }
    .months { display: flex; gap: 11px; margin: 5px 0 0 34px; }
    .months span { flex: 1; text-align: center; font-size: 10px; color: #55606b; white-space: nowrap; }
    .legend { display: flex; gap: 14px; margin: 7px 0 0 34px; font-size: 11px; color: #3c4650; }
    .legend i { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-inline-end: 4px; }
    .alerts h4 { margin-bottom: 12px; }
    .alert { display: flex; gap: 11px; padding: 9px 2px 11px; }
    .alert .sign { width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0; font-size: 13px; }
    .sign.red { background: #fde4e4; color: #d92d2d; }
    .sign.amber { background: #fdf2c9; color: #b98900; }
    .tags { display: flex; gap: 6px; margin-bottom: 3px; }
    .tag { padding: 0 7px; border-radius: 5px; border: 1px solid #e0e4e8; background: #f6f7f8; font-size: 10.5px; color: #3c4650; line-height: 18px; }
    .tag.red { background: #fdeeee; border-color: #f5c2c2; color: #cf2d2d; }
    .tag.amber { background: #fdf7df; border-color: #f0dc9c; color: #a87c00; }
    .alert p { margin: 0; font-size: 12px; color: #2d3742; line-height: 1.35; }
    @keyframes rise { from { transform: scaleY(0); } }
    @media (prefers-reduced-motion: reduce) { .month i { animation: none; } }
  `],
  template: `
    <aside>
      <div class="logo"><erp-brand-logo size="md" subtitle="Demo Workspace"></erp-brand-logo></div>
      <div class="menu-search">Search menu...</div>
      <div class="group">Workspace <span nz-icon nzType="down"></span></div>
      <div class="link on"><span nz-icon nzType="dashboard"></span>Overview</div>
      <div class="link"><span nz-icon nzType="check-square"></span>Approvals</div>
      @for (g of groups; track g) { <div class="group">{{ g }} <span nz-icon nzType="right"></span></div> }
    </aside>
    <main>
      <div class="bar">
        <span class="crumb">● WORKSPACE</span><span class="sep">/</span><strong>Overview</strong>
        <div class="tools">
          <span class="tool"><span nz-icon nzType="search"></span></span>
          <span class="tool add"><span nz-icon nzType="plus"></span></span>
          <span class="bell"><span nz-icon nzType="bell"></span><b>4</b></span>
          <span class="avatar">DW</span>
        </div>
      </div>
      <div class="page">
        <div class="range"><span class="select">Last year <span nz-icon nzType="down" style="font-size:10px"></span></span><span class="tool" style="width:28px;height:28px"><span nz-icon nzType="reload"></span></span> The last 12 months</div>
        <div class="tabs">
          <span class="all"><span nz-icon nzType="appstore"></span>All Overview <b>38</b></span>
          <span nz-icon nzType="dollar"></span><span nz-icon nzType="line-chart"></span><span nz-icon nzType="shopping"></span><span nz-icon nzType="project"></span><span nz-icon nzType="team"></span>
          <div class="switch"><span class="on">Curated Focus</span><span>All Metrics (38)</span></div>
        </div>
        <div class="label">Executive highlights</div>
        <div class="kpis">
          <div class="kpi" style="--c:#0f8a5a"><small>Collected in period</small><strong>USD 318,400</strong><em>receipts this period</em></div>
          <div class="kpi" style="--c:#2563eb"><small>Cash and bank</small><strong>USD 96,720</strong><em>across all accounts</em></div>
          <div class="kpi" style="--c:#0b83bd"><small>Receivables</small><strong>USD 42,150</strong><em>overdue 3</em></div>
          <div class="kpi" style="--c:#d9760b"><small>Net result for period</small><strong>USD 71,136</strong><em>income less expenses</em></div>
        </div>
        <div class="row">
          <div class="card">
            <div class="eyebrow"><span nz-icon nzType="dollar"></span>Finance &amp; Treasury</div>
            <h4>Billed vs collected</h4>
            <p class="sub">Invoices net of credit notes, and receipts, for the selected period · by month</p>
            <div class="pills"><span class="on">Billed vs collected</span><span>Receivables ageing</span><span>Expenses for period</span></div>
            <div class="plot">
              @for (t of ticks; track t.label) { <div class="tick" [style.bottom.%]="t.at"><span>{{ t.label }}</span></div> }
              @for (m of series; track $index) {
                <div class="month"><i class="a" [style.height.%]="m[0]" [style.animation-delay.ms]="$index * 55"></i><i class="b" [style.height.%]="m[1]" [style.animation-delay.ms]="$index * 55 + 40"></i></div>
              }
            </div>
            <div class="months">@for (m of months; track m) { <span>{{ m }}</span> }</div>
            <div class="legend"><span><i style="background:#10955d"></i>Billed</span><span><i style="background:#2563eb"></i>Collected</span></div>
          </div>
          <div class="card alerts">
            <h4>Alerts and to-dos</h4>
            <div class="alert"><span class="sign red"><span nz-icon nzType="warning"></span></span><div><div class="tags"><span class="tag">Finance</span><span class="tag red">Action needed</span></div><p>3 invoices are overdue</p></div></div>
            <div class="alert"><span class="sign amber"><span nz-icon nzType="bulb"></span></span><div><div class="tags"><span class="tag">Inventory</span><span class="tag red">Action needed</span></div><p>1 item at or below their reorder level</p></div></div>
            <div class="alert"><span class="sign amber"><span nz-icon nzType="bulb"></span></span><div><div class="tags"><span class="tag">HR</span><span class="tag amber">Upcoming</span></div><p>1 document expires within 30 days.</p></div></div>
            <div class="alert"><span class="sign amber"><span nz-icon nzType="check-square"></span></span><div><div class="tags"><span class="tag">Approvals</span><span class="tag amber">Upcoming</span></div><p>2 requests are waiting for your decision</p></div></div>
          </div>
        </div>
      </div>
    </main>`,
})
export class WebDashComponent {
  readonly groups = GROUPS;
  readonly months = lastMonths(12, true);
  readonly ticks = [{ label: '0', at: 0 }, { label: '10K', at: 20 }, { label: '20K', at: 40 }, { label: '30K', at: 60 }, { label: '40K', at: 80 }, { label: '50K', at: 100 }];
  readonly series = [[44, 40], [50, 46], [47, 49], [56, 50], [60, 58], [55, 57], [64, 60], [70, 62], [66, 69], [76, 70], [84, 74], [90, 86]];
}

import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, input, signal } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { InViewDirective, TiltDirective, prefersReducedMotion } from './pricing-fx';

export type PhoneScreen = 'overview' | 'invoices' | 'hr' | 'approvals' | 'alerts';

/** Where a phone sits in the carousel: 0 is at the front, then +1, -1 and so on, always the short way round. */
export function carouselOffset(index: number, active: number, count: number): number {
  let offset = (index - active) % count;
  if (offset > count / 2) offset -= count;
  if (offset < -count / 2) offset += count;
  return offset;
}

const PHONE_STYLES = `
  :host { display: block; width: 236px; height: 484px; transform-style: preserve-3d; }
  .body { position: relative; width: 100%; height: 100%; border-radius: 36px; background: linear-gradient(145deg, #2b3340, #0d1117); padding: 9px; box-shadow: 0 22px 34px -22px rgba(0, 0, 0, .55); transform-style: preserve-3d; }
  /* The back of the case, set behind the front: this is what gives the phone real thickness when it turns. */
  .body::before { content: ''; position: absolute; inset: 0; border-radius: 36px; background: #06090d; transform: translateZ(-12px); }
  .screen { position: relative; height: 100%; border-radius: 28px; overflow: hidden; background: #f5f6f8; color: #1b2430; font-size: 10px; display: flex; flex-direction: column; transform: translateZ(1px); text-align: start; }
  .screen::after { content: ''; position: absolute; inset: -40% -60%; background: linear-gradient(115deg, transparent 44%, rgba(255, 255, 255, .16) 50%, transparent 56%); pointer-events: none; animation: glare 7s ease-in-out infinite; }
  .status { display: flex; justify-content: space-between; padding: 8px 15px 0; font-size: 8.5px; color: #55606b; }
  .status .signal { letter-spacing: 1px; font-size: 7px; }
  header { display: flex; align-items: center; gap: 7px; padding: 8px 9px 7px; }
  header img { width: 22px; height: 22px; border-radius: 6px; }
  header div { flex: 1; min-width: 0; line-height: 1.15; }
  header strong { display: block; font-size: 12.5px; font-weight: 700; }
  header small { color: #66707c; font-size: 8.5px; }
  header .ic { position: relative; font-size: 12px; color: #2d3742; }
  header .ic b { position: absolute; top: -5px; right: -5px; min-width: 10px; height: 10px; border-radius: 9999px; background: #d92323; color: #fff; font-size: 6.5px; display: flex; align-items: center; justify-content: center; font-weight: 600; }
  header .me { position: relative; width: 19px; height: 19px; border-radius: 50%; background: #0b6b45; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 7.5px; font-weight: 700; }
  .chips { display: flex; gap: 4px; padding: 0 9px 7px; overflow: hidden; flex-shrink: 0; }
  .chips span { padding: 2px 7px; border: 1px solid #dde1e5; border-radius: 9999px; background: #fff; white-space: nowrap; font-size: 8.5px; color: #3c4650; }
  .chips span.on { background: #e6f5ee; border-color: #a8dcc3; color: #0b7a4b; font-weight: 600; }
  .chips b { font-weight: 500; color: #66707c; background: #eef0f2; border-radius: 9999px; padding: 0 3px; margin-inline-start: 2px; font-size: 7.5px; }
  .content { flex: 1; min-height: 0; padding: 0 9px; display: flex; flex-direction: column; gap: 6px; overflow: hidden; }
  .heading { display: flex; justify-content: space-between; font-size: 8px; font-weight: 600; letter-spacing: .05em; color: #66707c; }
  .heading span:last-child { font-weight: 500; letter-spacing: 0; }
  .card { background: #fff; border-radius: 9px; padding: 7px 8px; box-shadow: 0 1px 2px rgba(15, 23, 42, .06); flex-shrink: 0; }
  .stats { display: flex; gap: 6px; flex-shrink: 0; }
  .stat { position: relative; flex: 0 0 76%; padding: 6px 8px 6px 11px; background: #fff; border-radius: 9px; overflow: hidden; box-shadow: 0 1px 2px rgba(15, 23, 42, .06); }
  .stat::before { content: ''; position: absolute; inset-block: 0; left: 0; width: 3px; background: var(--c); }
  .stat small { display: block; font-size: 7.5px; font-weight: 600; letter-spacing: .04em; color: #55606b; white-space: nowrap; }
  .stat strong { display: block; font-size: 15px; font-weight: 800; color: var(--c); line-height: 1.25; }
  .stat em { font-style: normal; font-size: 8px; color: #66707c; white-space: nowrap; }
  .chart-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 4px; }
  .chart-head b { display: block; font-size: 10.5px; font-weight: 700; }
  .chart-head small { display: block; font-size: 7.5px; color: #66707c; white-space: nowrap; max-width: 104px; overflow: hidden; text-overflow: ellipsis; }
  .toggle { display: flex; border: 1px solid #dde1e5; border-radius: 9999px; overflow: hidden; font-size: 7.5px; font-weight: 600; }
  .toggle span { padding: 2px 6px; }
  .toggle .on { background: #d3e8dc; }
  .plot { position: relative; display: flex; align-items: flex-end; gap: 2px; height: 74px; margin: 7px 0 0 14px; border-bottom: 1px solid #e3e6e9; }
  .plot .tick { position: absolute; inset-inline: 0; border-top: 1px solid #f0f2f4; }
  .plot .tick span { position: absolute; left: -15px; top: -5px; font-size: 6.5px; color: #66707c; }
  .plot .m { flex: 1; height: 100%; display: flex; align-items: flex-end; gap: 1px; }
  .plot i { flex: 1; border-radius: 1.5px 1.5px 0 0; transform-origin: bottom; animation: grow .9s cubic-bezier(.2, .8, .2, 1) backwards; }
  .plot i.a { background: #2b7bdb; }
  .plot i.b { background: #ee6a36; }
  .axis { display: flex; gap: 2px; margin: 2px 0 0 14px; }
  .axis span { flex: 1; text-align: center; font-size: 5.5px; color: #66707c; overflow: hidden; }
  .legend { display: flex; align-items: center; gap: 7px; margin-top: 5px; font-size: 7.5px; color: #3c4650; white-space: nowrap; }
  .legend i { display: inline-block; width: 6px; height: 6px; border-radius: 1.5px; margin-inline-end: 2px; }
  .legend .pct { margin-inline-start: auto; padding: 0 5px; border: 1px solid #c5e6b8; background: #f0fae9; border-radius: 9999px; color: #2c7a11; font-weight: 600; }
  .inv { display: flex; justify-content: space-between; gap: 6px; }
  .inv .who { min-width: 0; }
  .inv .who b { display: block; font-size: 9.5px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .inv .who small { display: block; font-size: 8px; color: #66707c; line-height: 1.3; }
  .inv .amt { text-align: end; white-space: nowrap; }
  .inv .amt small { font-size: 7.5px; color: #66707c; }
  .inv .amt b { font-size: 10px; font-weight: 700; }
  .pill { display: inline-flex; align-items: center; gap: 3px; margin-top: 3px; padding: 0 6px; border-radius: 9999px; font-size: 8px; font-weight: 500; border: 1px solid; }
  .pill::before { content: ''; width: 4px; height: 4px; border-radius: 50%; background: currentColor; }
  .pill.paid { background: #f0fae9; border-color: #c5e6b8; color: #2c7a11; }
  .pill.issued { background: #e9f1fc; border-color: #bcd4f5; color: #1f5fbf; }
  .pill.overdue { background: #fdeee0; border-color: #f4cda6; color: #b35309; }
  .fab { position: absolute; right: 10px; bottom: 58px; display: flex; align-items: center; gap: 4px; padding: 6px 10px; border-radius: 10px; background: #10955d; color: #fff; font-size: 9.5px; font-weight: 600; box-shadow: 0 4px 10px rgba(16, 149, 93, .4); z-index: 2; }
  .line { display: flex; align-items: center; gap: 7px; }
  .line .grow { flex: 1; min-width: 0; }
  .line .name { font-weight: 600; font-size: 9.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .line .sub { color: #66707c; font-size: 8px; }
  .line .amount { font-weight: 700; font-size: 10px; white-space: nowrap; }
  .dot { width: 20px; height: 20px; border-radius: 6px; display: flex; align-items: center; justify-content: center; background: #e6f5ee; color: #0b7a4b; flex-shrink: 0; font-size: 10px; }
  .dot.warn { background: #fdeee0; color: #b35309; }
  .meter { height: 4px; border-radius: 9999px; background: #e8ecef; overflow: hidden; margin-top: 4px; }
  .meter i { display: block; height: 100%; background: #10955d; }
  .buttons { display: grid; grid-template-columns: 1fr 1fr; gap: 5px; margin-top: 6px; }
  .buttons span { text-align: center; padding: 3px; border-radius: 6px; font-weight: 600; font-size: 8.5px; }
  .approve { background: #10955d; color: #fff; }
  .decline { background: #eef1f3; color: #3c4650; }
  /* The app's floating menu, with the current page raised out of it. */
  nav { position: absolute; left: 7px; right: 7px; bottom: 9px; display: grid; grid-template-columns: repeat(5, 1fr); padding: 6px 1px 4px; border-radius: 12px; background: #fff; box-shadow: 0 2px 10px rgba(15, 23, 42, .1); z-index: 3; }
  nav span.item { position: relative; display: flex; flex-direction: column; align-items: center; gap: 1px; font-size: 6.5px; color: #66707c; }
  nav .item > [nz-icon] { font-size: 12px; }
  nav span.on { color: #1b2430; font-weight: 700; }
  nav span.on > [nz-icon] { width: 25px; height: 25px; margin-top: -14px; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: #10955d; background: #e6f5ee; box-shadow: 0 0 0 3px #fff; font-size: 13px; }
  .pad { height: 40px; flex-shrink: 0; }
  @keyframes grow { from { transform: scaleY(0); } }
  @keyframes glare { 0%, 55% { transform: translateX(-30%); } 85%, 100% { transform: translateX(30%); } }
  @media (prefers-reduced-motion: reduce) { .screen::after, .plot i { animation: none; } }
`;

/** The last few months, oldest first, as the app's charts label them. */
export function lastMonths(count: number, withYear = false, now = new Date()): string[] {
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (count - i), 1);
    const name = d.toLocaleString('en-US', { month: 'short' });
    return withYear ? `${name} ${String(d.getFullYear()).slice(2)}` : name;
  });
}

/** One phone showing a screen of the mobile app: its real header, chips, cards and floating menu, with sample figures. A drawing rather than a screenshot, so it stays sharp at any size. */
@Component({
  selector: 'erp-phone',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule],
  styles: [PHONE_STYLES],
  template: `
    <div class="body">
      <div class="screen">
        <div class="status"><span>9:41</span><span class="signal">▲ ▮▮▮</span></div>
        <header>
          <img src="highersquare-emblem.png" alt="" />
          <div><strong>{{ title() }}</strong><small>Demo Workspace</small></div>
          <span class="ic" nz-icon nzType="search"></span>
          <span class="ic"><span nz-icon nzType="bell"></span><b>4</b></span>
          <span class="me">DW</span>
        </header>
        @switch (screen()) {
          @case ('overview') {
            <div class="chips"><span class="on">All <b>38</b></span><span>Finance <b>6</b></span><span>Sales <b>4</b></span><span>Approvals <b>1</b></span></div>
            <div class="content">
              <div class="heading"><span>HIGHLIGHTS</span><span>38 metrics</span></div>
              <div class="stats">
                <div class="stat" style="--c:#7c3aed"><small>PENDING APPROVALS</small><strong>2</strong><em>waiting for your decision</em></div>
                <div class="stat" style="--c:#0b83bd"><small>RECEIVABLES</small><strong>42,150</strong><em>overdue 3</em></div>
              </div>
              <div class="card">
                <div class="chart-head"><div><b>Billed vs collected</b><small>Invoices net of credit notes, and receipts</small></div><div class="toggle"><span class="on">Bar</span><span>Line</span></div></div>
                <div class="plot">
                  <div class="tick" style="bottom:50%"><span>30K</span></div><div class="tick" style="bottom:100%"><span>60K</span></div>
                  @for (b of bars; track $index) { <div class="m"><i class="a" [style.height.%]="b[0]" [style.animation-delay.ms]="$index * 50"></i><i class="b" [style.height.%]="b[1]" [style.animation-delay.ms]="$index * 50 + 30"></i></div> }
                </div>
                <div class="axis">@for (m of months; track m) { <span>{{ m }}</span> }</div>
                <div class="legend"><span><i style="background:#2b7bdb"></i>Billed 381K</span><span><i style="background:#ee6a36"></i>Collected 362K</span><span class="pct">95%</span></div>
              </div>
              <div class="card"><div class="chart-head"><div><b>Receivables ageing</b><small>Open invoice balances by days past due</small></div></div></div>
            </div>
          }
          @case ('invoices') {
            <div class="chips"><span class="on">All</span><span>Draft</span><span>Issued</span><span>Partly paid</span><span>Paid</span></div>
            <div class="content">
              @for (i of invoices; track i.no) {
                <div class="card inv"><div class="who"><b>{{ i.who }}</b><small>{{ i.no }}</small><small>Due {{ i.due }}</small></div><div class="amt"><small>USD </small><b>{{ i.amt }}</b><br /><span class="pill" [class]="'pill ' + i.tone">{{ i.state }}</span></div></div>
              }
            </div>
            <span class="fab"><span nz-icon nzType="plus"></span>New</span>
          }
          @case ('hr') {
            <div class="content" style="padding-top:2px">
              <div class="card line"><span class="me-tile dot"><span nz-icon nzType="user"></span></span><div class="grow"><div class="name">Your profile</div><div class="sub">Employee EMP-0014</div></div></div>
              <div class="heading"><span>LEAVE BALANCES</span></div>
              <div class="card"><div class="line"><div class="grow"><div class="name">Annual leave</div></div><span class="sub">14 of 21 days</span></div><div class="meter"><i style="width:66%"></i></div></div>
              <div class="card"><div class="line"><div class="grow"><div class="name">Sick leave</div></div><span class="sub">8 of 10 days</span></div><div class="meter"><i style="width:80%"></i></div></div>
              <div class="heading"><span>MY LEAVE</span></div>
              <div class="card line"><span class="dot"><span nz-icon nzType="calendar"></span></span><div class="grow"><div class="name">12 Mar - 14 Mar</div><div class="sub">Annual leave</div></div><span class="pill issued">Waiting for approval</span></div>
              <div class="card line"><span class="dot"><span nz-icon nzType="file-text"></span></span><div class="grow"><div class="name">Payslips</div><div class="sub">See and download yours</div></div></div>
            </div>
            <span class="fab"><span nz-icon nzType="plus"></span>Request leave</span>
          }
          @case ('approvals') {
            <div class="chips"><span class="on">All <b>3</b></span><span>Purchasing <b>1</b></span><span>Projects <b>1</b></span><span>HR <b>1</b></span></div>
            <div class="content">
              <div class="card"><div class="line"><div class="grow"><div class="name">Purchase order PO-1042</div><div class="sub">Office supplies</div></div><span class="amount">3,200</span></div><div class="buttons"><span class="approve">Approve</span><span class="decline">Reject</span></div></div>
              <div class="card"><div class="line"><div class="grow"><div class="name">Project expense</div><div class="sub">Site visit travel</div></div><span class="amount">480</span></div><div class="buttons"><span class="approve">Approve</span><span class="decline">Reject</span></div></div>
              <div class="card"><div class="line"><div class="grow"><div class="name">Leave request</div><div class="sub">Three days in March</div></div></div><div class="buttons"><span class="approve">Approve</span><span class="decline">Reject</span></div></div>
            </div>
          }
          @case ('alerts') {
            <div class="content" style="padding-top:2px">
              <div class="heading"><span>NEEDS ATTENTION</span></div>
              @for (n of alerts; track n.title) {
                <div class="card line"><span class="dot" [class.warn]="n.warn"><span nz-icon [nzType]="n.icon"></span></span><div class="grow"><div class="name" style="white-space:normal">{{ n.title }}</div><div class="sub">{{ n.when }}</div></div></div>
              }
            </div>
          }
        }
        <div class="pad"></div>
        <nav>
          <span class="item" [class.on]="screen() === 'overview'"><span nz-icon nzType="appstore"></span>Overview</span>
          <span class="item" [class.on]="screen() === 'invoices'"><span nz-icon nzType="file-text"></span>Invoices</span>
          <span class="item"><span nz-icon nzType="wallet"></span>Receipts</span>
          <span class="item"><span nz-icon nzType="team"></span>Customers</span>
          <span class="item" [class.on]="screen() === 'hr' || screen() === 'approvals' || screen() === 'alerts'"><span nz-icon nzType="appstore-add"></span>More</span>
        </nav>
      </div>
    </div>`,
})
export class PhoneComponent {
  readonly screen = input<PhoneScreen>('overview');
  readonly title = computed(() => ({ overview: 'Overview', invoices: 'Invoices', hr: 'My HR', approvals: 'Approvals', alerts: 'Notifications' })[this.screen()]);
  readonly months = lastMonths(12);
  readonly bars = [[44, 40], [50, 46], [47, 49], [56, 50], [60, 58], [55, 57], [64, 60], [70, 62], [66, 69], [76, 70], [84, 74], [90, 86]];
  readonly invoices = [
    { who: 'Northwind Trading', no: 'INV-2026-07-017', due: '25 Jul 2026', amt: '1,050.00', state: 'Paid', tone: 'paid' },
    { who: 'Blue Harbor Foods', no: 'INV-2026-06-018', due: '25 Jun 2026', amt: '2,480.00', state: 'Issued', tone: 'issued' },
    { who: 'Atlas Supplies', no: 'INV-2026-05-019', due: '25 May 2026', amt: '640.00', state: 'Overdue', tone: 'overdue' },
    { who: 'Cedar & Co', no: 'INV-2026-04-020', due: '25 Apr 2026', amt: '3,115.50', state: 'Paid', tone: 'paid' },
    { who: 'Summit Works', no: 'INV-2026-03-021', due: '25 Mar 2026', amt: '915.00', state: 'Paid', tone: 'paid' },
  ];
  readonly alerts = [
    { icon: 'check-circle', title: 'Receipt recorded for INV-2026-07-017', when: 'A minute ago', warn: false },
    { icon: 'check-square', title: 'A purchase order needs your approval', when: '20 minutes ago', warn: false },
    { icon: 'warning', title: '3 invoices are overdue', when: 'This morning', warn: true },
    { icon: 'calendar', title: 'Your leave request was approved', when: 'Yesterday', warn: false },
  ];
}

interface Slide { key: PhoneScreen; title: string; text: string; icon: string; }

/** Only what the mobile app really does. */
const SLIDES: Slide[] = [
  { key: 'overview', icon: 'dashboard', title: 'The business at a glance', text: 'Sales, what customers owe and what needs attention, on the same figures as the office.' },
  { key: 'invoices', icon: 'file-text', title: 'Invoices, receipts and customers', text: 'Look up a customer or invoice while you are with them, and record what they paid.' },
  { key: 'approvals', icon: 'check-square', title: 'Approve from anywhere', text: 'Purchase orders, expenses and leave do not have to wait until you are back at a desk.' },
  { key: 'hr', icon: 'calendar', title: 'My HR for every employee', text: 'People check their leave balance, ask for time off and see their payslips themselves.' },
  { key: 'alerts', icon: 'bell', title: 'Your notifications, wherever you are', text: 'See what needs you from the list in the app, signed in as safely as on the web, with two-factor codes.' },
];
const AUTOPLAY_MS = 3800;

/** The mobile app, presented as phones on a turning 3D carousel. Choosing a line or a phone turns it to the front. */
@Component({
  selector: 'erp-pricing-mobile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule, PhoneComponent, InViewDirective, TiltDirective],
  styles: [`
    :host { display: block; }
    .grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.25fr); gap: 40px; align-items: center; }
    .eyebrow { display: inline-block; padding: 4px 12px; border-radius: 9999px; background: rgba(255, 255, 255, .12); border: 1px solid rgba(255, 255, 255, .25); font-size: 13px; font-weight: 600; margin-bottom: 16px; color: #d7f5e6; }
    h2 { font-size: 32px; font-weight: 800; letter-spacing: -.02em; line-height: 1.18; margin: 0 0 12px; color: #fff; }
    .lead { color: #cfe6da; font-size: 16px; line-height: 1.55; margin: 0 0 22px; }
    .list { display: flex; flex-direction: column; gap: 6px; }
    .item { display: flex; gap: 12px; width: 100%; text-align: start; padding: 11px 12px; border-radius: 12px; border: 1px solid transparent; background: none; color: inherit; cursor: pointer; font: inherit; transition: background .25s ease, border-color .25s ease, transform .25s ease; }
    .item:hover { background: rgba(255, 255, 255, .06); }
    .item:focus-visible { outline: 2px solid #fff; outline-offset: 2px; }
    .item.on { background: rgba(255, 255, 255, .12); border-color: rgba(255, 255, 255, .28); transform: translateX(4px); }
    .ico { display: flex; align-items: center; justify-content: center; width: 34px; height: 34px; border-radius: 9px; background: rgba(255, 255, 255, .12); color: #9de8c4; flex-shrink: 0; font-size: 16px; }
    .item.on .ico { background: #fff; color: #0f6b47; }
    .item strong { display: block; color: #fff; font-size: 14.5px; font-weight: 700; }
    .item span.text { display: grid; grid-template-rows: 0fr; transition: grid-template-rows .3s ease; color: #cfe6da; font-size: 13.5px; line-height: 1.45; }
    .item span.text > span { overflow: hidden; }
    .item.on span.text { grid-template-rows: 1fr; }

    /* Faded at the edges, so phones leaving the stage melt away instead of being cut off. */
    .stage { position: relative; height: 560px; perspective: 1300px; --tilt-x: 0deg; --tilt-y: 0deg; touch-action: pan-y; overflow: hidden;
      -webkit-mask-image: linear-gradient(to right, transparent, #000 10%, #000 90%, transparent), linear-gradient(to bottom, #000 90%, transparent);
      -webkit-mask-composite: source-in;
      mask-image: linear-gradient(to right, transparent, #000 10%, #000 90%, transparent), linear-gradient(to bottom, #000 90%, transparent);
      mask-composite: intersect; }
    .turntable { position: absolute; inset: 0; transform-style: preserve-3d; transform: rotateX(calc(3deg + var(--tilt-x))) rotateY(var(--tilt-y)); transition: transform .35s ease-out; }
    .floor { position: absolute; left: 50%; bottom: 8px; width: 380px; height: 70px; margin-left: -190px; border-radius: 50%; background: radial-gradient(closest-side, rgba(157, 232, 196, .45), transparent); }
    .slot { position: absolute; left: 50%; top: 24px; width: 236px; margin-left: -118px; transform-style: preserve-3d; cursor: pointer; transition: transform .8s cubic-bezier(.22, .9, .25, 1), opacity .6s ease; }
    .slot .bob { position: relative; transform-style: preserve-3d; }
    /* Phones further back sit in shade rather than turning see-through, which would show the phone behind them. */
    .slot .bob::after { content: ''; position: absolute; inset: 0; border-radius: 36px; background: #04231a; opacity: var(--dim, 0); transform: translateZ(3px); transition: opacity .7s ease; pointer-events: none; }
    .slot.front { cursor: default; }
    .slot.front .bob { animation: bob 5s ease-in-out infinite; }
    .fx-wait .slot { opacity: 0 !important; }
    .note { margin: 14px 0 0; font-size: 12px; color: #a9c9b9; }
    @keyframes bob { 0%, 100% { transform: translateY(0) rotateY(-3deg); } 50% { transform: translateY(-10px) rotateY(3deg); } }
    @media (max-width: 1023px) { .grid { grid-template-columns: minmax(0, 1fr); gap: 24px; } }
    @media (max-width: 767px) {
      h2 { font-size: 25px; }
      .stage { height: 520px; }
      .floor { width: 280px; margin-left: -140px; }
    }
    @media (prefers-reduced-motion: reduce) { .slot, .item, .item span.text, .turntable { transition: none; } .slot.front .bob { animation: none; } }
  `],
  template: `
    <div class="grid">
      <div>
        <span class="eyebrow">Mobile app</span>
        <h2>Your business, in your pocket</h2>
        <p class="lead">The same workspace and the same figures, on your phone. Sign in with your usual account and carry on from where you left off.</p>
        <div class="list" (mouseenter)="hold(true)" (mouseleave)="hold(false)" (focusin)="hold(true)" (focusout)="hold(false)" data-testid="pricing-mobile-list">
          @for (s of slides; track s.key; let i = $index) {
            <button type="button" class="item" [class.on]="i === active()" [attr.aria-pressed]="i === active()" (click)="select(i)" [attr.data-testid]="'pricing-mobile-' + s.key">
              <span class="ico" nz-icon [nzType]="s.icon"></span>
              <span><strong>{{ s.title }}</strong><span class="text"><span>{{ s.text }}</span></span></span>
            </button>
          }
        </div>
        <p class="note">Screens follow the real app. Names and figures are samples.</p>
      </div>
      <div class="stage" erpInView erpTilt aria-hidden="true" (mouseenter)="hold(true)" (mouseleave)="hold(false)" data-testid="pricing-mobile-stage">
        <div class="turntable">
          <div class="floor"></div>
          @for (s of slides; track s.key; let i = $index) {
            <div class="slot" [class.front]="i === active()" [style.transform]="placed()[i].transform" [style.opacity]="placed()[i].opacity" [style.--dim]="placed()[i].dim" (click)="select(i)">
              <div class="bob"><erp-phone [screen]="s.key" /></div>
            </div>
          }
        </div>
      </div>
    </div>`,
})
export class PricingMobileComponent implements OnInit, OnDestroy {
  readonly slides = SLIDES;
  readonly active = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;
  private held = false;

  readonly placed = computed(() => {
    const narrow = typeof window !== 'undefined' && window.innerWidth < 768;
    const spread = narrow ? 118 : 178;
    return this.slides.map((_, i) => {
      const offset = carouselOffset(i, this.active(), this.slides.length);
      const away = Math.abs(offset);
      return {
        transform: `translateX(${offset * spread}px) translateZ(${-away * 150}px) rotateY(${-offset * 32}deg) scale(${narrow ? 0.94 : 1})`,
        opacity: 1,
        dim: away === 0 ? 0 : away === 1 ? 0.32 : 0.62,
      };
    });
  });

  ngOnInit(): void {
    if (prefersReducedMotion()) return;
    this.timer = setInterval(() => {
      if (!this.held && !document.hidden) this.active.update((a) => (a + 1) % this.slides.length);
    }, AUTOPLAY_MS);
  }

  ngOnDestroy(): void { if (this.timer) clearInterval(this.timer); }

  select(i: number): void { this.active.set(i); }
  hold(on: boolean): void { this.held = on; }
}

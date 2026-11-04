import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { OrgChartApi, OrgChartDto } from './hr-people.api';

/** The organisation: departments with their people, and the reporting lines (who reports to whom). */
@Component({
  selector: 'erp-org-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, NzTabsModule],
  styles: [`
    ul.tree { list-style: none; margin: 0; padding-inline-start: 22px; border-inline-start: 1px solid var(--erp-color-border, #e2e8f0); }
    ul.tree.root { padding-inline-start: 0; border: 0; }
    li { margin: 6px 0; }
    .node { display: inline-flex; flex-direction: column; padding: 6px 12px; border: 1px solid var(--erp-color-border, #e2e8f0); border-radius: 8px; }
    .node small, .members { color: var(--erp-color-text-secondary, #64748b); font-size: 12px; }
    .members { margin: 4px 0 0; }
  `],
  template: `
    <h1 class="erp-page-title">Organisation</h1>
    @if (chart(); as c) {
      <nz-tabs>
        <nz-tab nzTitle="Departments">
          <ul class="tree root" data-testid="org-departments">
            <ng-container *ngTemplateOutlet="dept; context: { $implicit: c.departments }"></ng-container>
          </ul>
          @if (c.unassigned.length) {
            <h3 style="margin-top:16px">No department</h3>
            <p class="members">{{ names(c.unassigned) }}</p>
          }
        </nz-tab>
        <nz-tab nzTitle="Reporting lines">
          <ul class="tree root" data-testid="org-reporting">
            <ng-container *ngTemplateOutlet="person; context: { $implicit: c.reportingLines }"></ng-container>
          </ul>
        </nz-tab>
      </nz-tabs>
    } @else { <p>Loading…</p> }

    <ng-template #dept let-nodes>
      @for (d of nodes; track d.id) {
        <li>
          <span class="node"><b>{{ d.name }}</b><small>{{ d.code }} · {{ d.members.length }} people</small></span>
          @if (d.members.length) { <div class="members">{{ names(d.members) }}</div> }
          @if (d.children.length) { <ul class="tree"><ng-container *ngTemplateOutlet="dept; context: { $implicit: d.children }"></ng-container></ul> }
        </li>
      }
    </ng-template>
    <ng-template #person let-nodes>
      @for (p of nodes; track p.id) {
        <li>
          <span class="node"><b>{{ p.name }}</b><small>{{ p.designation }} · {{ p.number }}</small></span>
          @if (p.reports.length) { <ul class="tree"><ng-container *ngTemplateOutlet="person; context: { $implicit: p.reports }"></ng-container></ul> }
        </li>
      }
    </ng-template>`,
})
export class OrgChartPage {
  readonly chart = signal<OrgChartDto | null>(null);
  constructor() { inject(OrgChartApi).get().subscribe((c) => this.chart.set(c)); }
  names(list: { name: string }[]): string { return list.map((m) => m.name).join(', '); }
}

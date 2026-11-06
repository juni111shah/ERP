import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { OrgChartApi, OrgChartDto } from './hr-people.api';

/** The organisation: departments with their people, and the reporting lines (who reports to whom). */
@Component({
  selector: 'erp-org-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, NzSkeletonModule, NzTabsModule, StatePanelComponent],
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
    @if (failed()) {
      <erp-state-panel kind="error" title="The organisation could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else if (chart(); as c) {
      <nz-tabs>
        <nz-tab nzTitle="Departments">
          @if (!c.departments.length && !c.unassigned.length) { <erp-state-panel title="No departments yet" hint="Add departments and choose one on each employee record to see the structure here." icon="apartment" /> }
          <ul class="tree root" data-testid="org-departments">
            <ng-container *ngTemplateOutlet="dept; context: { $implicit: c.departments }"></ng-container>
          </ul>
          @if (c.unassigned.length) {
            <h3 style="margin-top:16px">No department</h3>
            <p class="members">{{ names(c.unassigned) }}</p>
          }
        </nz-tab>
        <nz-tab nzTitle="Reporting lines">
          @if (!c.reportingLines.length) { <erp-state-panel title="No reporting lines yet" hint="Choose a line manager on each employee record to see who reports to whom." icon="apartment" /> }
          <ul class="tree root" data-testid="org-reporting">
            <ng-container *ngTemplateOutlet="person; context: { $implicit: c.reportingLines }"></ng-container>
          </ul>
        </nz-tab>
      </nz-tabs>
    } @else { <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }"></nz-skeleton> }

    <ng-template #dept let-nodes>
      @for (d of nodes; track d.id) {
        <li>
          <span class="node"><b>{{ d.name }}</b><small>{{ d.code }} · {{ d.members.length }} {{ d.members.length === 1 ? 'person' : 'people' }}</small></span>
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
  private readonly api = inject(OrgChartApi);
  readonly chart = signal<OrgChartDto | null>(null);
  readonly failed = signal(false);
  constructor() { this.load(); }
  load(): void { this.failed.set(false); this.api.get().subscribe({ next: (c) => this.chart.set(c), error: () => this.failed.set(true) }); }
  names(list: { name: string }[]): string { return list.map((m) => m.name).join(', '); }
}

import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { SessionStore } from '@core/session/session.store';
import { ProjectExpensesPage } from '../../pages/project-expenses/project-expenses.page';
import { ProjectTimesheetsPage } from '../../pages/project-timesheets/project-timesheets.page';
import { PRJ_SUBTABS } from '../../projects-ui';

/** Time and expenses of a project. */
@Component({
  selector: 'erp-project-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectTimesheetsPage, ProjectExpensesPage],
  template: `
    <div class="seg" role="group" aria-label="View">
      @if (canTime()) { <button type="button" [class.on]="view() === 'time'" (click)="view.set('time')" data-testid="time-tab">Time</button> }
      @if (canCost()) { <button type="button" [class.on]="view() === 'expenses'" (click)="view.set('expenses')" data-testid="expenses-tab">Expenses</button> }
    </div>
    @if (view() === 'time') { <erp-project-timesheets [projectId]="projectId()" /> } @else { <erp-project-expenses [projectId]="projectId()" /> }
  `,
  styles: [PRJ_SUBTABS],
})
export class ProjectTimeComponent {
  readonly projectId = input.required<string>();
  private readonly session = inject(SessionStore);
  readonly canTime = computed(() => this.session.hasPermission('projects.timesheets.read'));
  readonly canCost = computed(() => this.session.hasPermission('projects.expenses.read'));
  readonly view = signal<'time' | 'expenses'>(this.session.hasPermission('projects.timesheets.read') ? 'time' : 'expenses');
}

import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { SessionStore } from '@core/session/session.store';
import { ProjectChangeOrdersPage } from '../../pages/project-change-orders/project-change-orders.page';
import { ProjectRisksPage } from '../../pages/project-risks/project-risks.page';
import { PRJ_SUBTABS } from '../../projects-ui';

/** Risks and change orders of a project. */
@Component({
  selector: 'erp-project-control',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectRisksPage, ProjectChangeOrdersPage],
  template: `
    <div class="seg" role="group" aria-label="View">
      @if (canRisks()) { <button type="button" [class.on]="view() === 'risks'" (click)="view.set('risks')" data-testid="control-risks">Risks and issues</button> }
      @if (canChanges()) { <button type="button" [class.on]="view() === 'changes'" (click)="view.set('changes')" data-testid="control-changes">Change orders</button> }
    </div>
    @if (view() === 'risks') { <erp-project-risks [projectId]="projectId()" /> } @else { <erp-project-change-orders [projectId]="projectId()" /> }
  `,
  styles: [PRJ_SUBTABS],
})
export class ProjectControlComponent {
  readonly projectId = input.required<string>();
  private readonly session = inject(SessionStore);
  readonly canRisks = computed(() => this.session.hasPermission('projects.risks.read'));
  readonly canChanges = computed(() => this.session.hasPermission('projects.changeorders.read'));
  readonly view = signal<'risks' | 'changes'>(this.session.hasPermission('projects.risks.read') ? 'risks' : 'changes');
}

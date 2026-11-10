import { ChangeDetectionStrategy, Component, input, output, signal, viewChild } from '@angular/core';
import { ProjectDto } from '@core/api/projects.api';
import { ProjectBoardPage } from '../../pages/project-board/project-board.page';
import { ProjectTasksPage } from '../../pages/project-tasks/project-tasks.page';
import { PRJ_SUBTABS } from '../../projects-ui';
import { ProjectGanttComponent } from '../project-gantt/project-gantt.component';
import { TaskDrawerComponent } from '../task-drawer/task-drawer.component';

/** The tasks of a project as a list, a board or a timeline. */
@Component({
  selector: 'erp-project-work',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectTasksPage, ProjectBoardPage, ProjectGanttComponent, TaskDrawerComponent],
  templateUrl: './project-work.component.html',
  styles: [PRJ_SUBTABS],
})
export class ProjectWorkComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly gantt = viewChild(ProjectGanttComponent);
  readonly views = [{ key: 'list', label: 'List' }, { key: 'board', label: 'Board' }, { key: 'timeline', label: 'Timeline' }];
  readonly view = signal(this.read());
  private read(): string { try { return localStorage.getItem('erp.projects.workview') ?? 'list'; } catch { return 'list'; } }
  setView(v: string): void { this.view.set(v); try { localStorage.setItem('erp.projects.workview', v); } catch { /* storage may be blocked */ } }
  reloadGantt(): void { this.gantt()?.load(); }
}

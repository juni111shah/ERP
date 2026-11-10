import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { EmployeeLifeApi, OpenTask } from '@core/api/hr-people.api';
import { asProblem } from '@core/http/problem-details';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Onboarding and offboarding steps nobody has finished yet, overdue ones first. */
@Component({
  selector: 'erp-open-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, HasPermissionDirective, StatePanelComponent, NzTableModule, NzTagModule],
  templateUrl: './open-tasks.page.html',
})
export class OpenTasksPage {
  private readonly lifeApi = inject(EmployeeLifeApi);
  private readonly message = inject(NzMessageService);
  readonly tasks = signal<OpenTask[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);

  constructor() { this.load(); }

  done(t: OpenTask): void {
    this.lifeApi.setDone(t.id, true).subscribe({ next: () => this.load(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the task') });
  }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.lifeApi.openTasks().subscribe({ next: (r) => { this.tasks.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
  }
}

import { doneText } from '@core/format/done-text';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProjectBillingType, ProjectsApi } from '@core/api/projects.api';
import { SessionStore } from '@core/session/session.store';

/**
 * "Create project" for a record of another module (a sales order, an opportunity ...): opens a small form, creates the project with the
 * record as its source and the same customer, and goes to it. Shown only when the Projects module is on and the person may create projects.
 */
@Component({
  selector: 'erp-create-project-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule],
  template: `
    @if (visible()) {
      <button nz-button (click)="open.set(true)" data-testid="create-project"><span nz-icon nzType="project"></span> Create project</button>
      <nz-modal [nzVisible]="open()" nzTitle="Create a project from this record" nzOkText="Create project" [nzOkLoading]="busy()" (nzOnOk)="create()" (nzOnCancel)="open.set(false)">
        <ng-container *nzModalContent>
          <label>Project name<input nz-input [(ngModel)]="name" maxlength="200" aria-label="Project name" data-testid="cp-name" /></label>
          <label style="display:block;margin-top:8px">How is it billed
            <nz-select [(ngModel)]="billingType" style="width:100%" aria-label="Billing type">
              <nz-option nzValue="timeAndMaterials" nzLabel="Time and materials"></nz-option>
              <nz-option nzValue="fixedFee" nzLabel="Fixed fee (the budget is the fee)"></nz-option>
              <nz-option nzValue="nonBillable" nzLabel="Not billed"></nz-option>
            </nz-select>
          </label>
          <p class="hint">Customer, budget and the link back to {{ sourceLabel() }} are filled in for you.</p>
        </ng-container>
      </nz-modal>
    }
  `,
  styles: ['.hint { color:#64748b; font-size:12.5px; margin-top:8px }'],
})
export class CreateProjectButtonComponent {
  private readonly session = inject(SessionStore);
  private readonly api = inject(ProjectsApi);
  private readonly router = inject(Router);
  private readonly msg = inject(NzMessageService);

  readonly sourceType = input.required<string>();
  readonly sourceId = input.required<string>();
  readonly sourceLabel = input<string>('this record');
  readonly defaultName = input<string>('');
  readonly legalEntityId = input.required<string>();
  readonly customerId = input<string | null | undefined>(null);
  readonly amount = input<number>(0);

  readonly open = signal(false);
  readonly busy = signal(false);
  readonly visible = computed(() => this.session.isModuleEnabled('projects') && this.session.hasPermission('projects.projects.manage'));
  name = '';
  billingType: ProjectBillingType = 'timeAndMaterials';

  create(): void {
    const name = (this.name || this.defaultName()).trim();
    if (!name) { this.msg.warning('Give the project a name.'); return; }
    this.busy.set(true);
    this.api.create({
      legalEntityId: this.legalEntityId(), name, status: 'planning', billingType: this.billingType, budgetAmount: this.amount() || 0,
      customerId: this.customerId() ?? null, sourceType: this.sourceType(), sourceId: this.sourceId(),
    }).subscribe({
      next: (p) => { this.busy.set(false); this.open.set(false); this.msg.success(doneText('created', 'Project', (p as { number?: string }).number)); void this.router.navigate(['/projects', (p as { id: string }).id]); },
      error: (e) => { this.busy.set(false); this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'Could not create the project.'); },
    });
  }
}

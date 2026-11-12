import { signal } from '@angular/core';
import { Observable } from 'rxjs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ProblemDetails } from '@core/api/api.models';
import { asProblem } from '@core/http/problem-details';
import { tNow } from '@core/i18n/translate-now';

export interface ActionFormDeps<T = unknown> {
  /** the call the form makes; receives the form values and the record the form was opened for */
  run: (payload: Record<string, unknown>, target: T | null) => Observable<unknown>;
  /** the success message; also called with the response */
  success: (response: unknown, payload: Record<string, unknown>, target: T | null) => string;
  /** runs after a success (reload the list or detail) */
  done: (response: unknown) => void;
  message: NzMessageService;
  /** shown when the server sends no explanation */
  failure?: string;
}

/**
 * Open / saving / errors state of a drawer that runs one action (close a year, dispose of an asset, restock a return) rather than
 * saving a master-data record. The drawer is an <erp-record-drawer>; field errors from the server land on the right field.
 */
export class ActionFormState<T = unknown> {
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly target = signal<T | null>(null);

  constructor(private readonly deps: ActionFormDeps<T>) {}

  show(target: T | null = null): void {
    this.errors.set(null);
    this.target.set(target);
    this.open.set(true);
  }

  close(): void { this.open.set(false); }

  submit(payload: Record<string, unknown>): void {
    this.saving.set(true);
    this.errors.set(null);
    const target = this.target();
    this.deps.run(payload, target).subscribe({
      next: (response) => {
        this.saving.set(false);
        this.open.set(false);
        this.deps.message.success(this.deps.success(response, payload, target));
        this.deps.done(response);
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        const hasFieldErrors = !!problem?.errors && Object.keys(problem.errors).length > 0;
        if (!hasFieldErrors) this.deps.message.error(problem?.detail ?? problem?.title ?? tNow('ui.msg.actionFailed', this.deps.failure ?? 'Could not complete this action'));
      },
    });
  }
}

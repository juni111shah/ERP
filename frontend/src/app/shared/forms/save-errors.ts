import { WritableSignal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ProblemDetails } from '@core/api/api.models';
import { applyProblemDetailsToForm, asProblem, problemMessage } from '@core/http/problem-details';

/**
 * A failed save from an `<erp-record-drawer>`: the problem goes to the drawer (`errors`), which puts field errors on their fields;
 * a problem without field errors becomes a toast instead.
 */
export function showDrawerSaveError(error: unknown, errors: WritableSignal<ProblemDetails | null>, message: NzMessageService, fallback: string): void {
  const problem = asProblem(error);
  errors.set(problem);
  if (!problem?.errors) message.error(problemMessage(error, fallback));
}

/**
 * A failed save of a reactive form: server field errors land on their controls, and the first error that matches no control
 * (else the problem's own text, else `fallback`) becomes a toast.
 */
export function showFormSaveError(form: FormGroup, error: unknown, message: NzMessageService, fallback: string): void {
  const problem = asProblem(error);
  const rest = problem ? applyProblemDetailsToForm(form, problem) : [];
  message.error(rest[0] ?? problem?.detail ?? problem?.title ?? fallback);
}

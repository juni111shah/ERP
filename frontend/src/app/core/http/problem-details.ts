import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';
import { ProblemDetails } from '../api/api.models';

export function asProblem(err: unknown): ProblemDetails | null {
  if (err instanceof HttpErrorResponse && err.error && typeof err.error === 'object') {
    return { status: err.status, ...(err.error as ProblemDetails) };
  }
  return err instanceof HttpErrorResponse ? { status: err.status, title: err.statusText } : null;
}

/**
 * One user-facing message for a failed call: the first message for `field` in errors (case-insensitive), else the first
 * field error, else detail, else title, else the fallback. Used where a 400 carries errors.reason (void) or a 409 a detail.
 */
export function problemMessage(err: unknown, fallback: string, field?: string): string {
  const p = asProblem(err);
  const errors = p?.errors ?? {};
  const key = field ? Object.keys(errors).find((k) => k.toLowerCase() === field.toLowerCase()) : undefined;
  const msgs = key ? errors[key] : Object.values(errors)[0];
  return msgs?.[0] ?? p?.detail ?? p?.title ?? fallback;
}

/**
 * Maps ProblemDetails.errors onto form controls (spec 10.2 "server errors mapped to fields").
 * Keys are dot paths: "code", "customFields.floorCount". Returns messages that matched no control (show as toast/alert).
 */
export function applyProblemDetailsToForm(form: FormGroup, problem: ProblemDetails): string[] {
  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const control = form.get(key);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
      control.markAsDirty();
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

/** Human message for the first error on a control (used by nz-form-control [nzErrorTip]). */
export function controlError(control: AbstractControl | null): string {
  const e = control?.errors;
  if (!e) return '';
  if (e['server']) return e['server'];
  if (e['required']) return 'Required';
  if (e['maxlength']) return `Max ${e['maxlength'].requiredLength} characters`;
  if (e['minlength']) return `Min ${e['minlength'].requiredLength} characters`;
  if (e['min']) return `Must be ≥ ${e['min'].min}`;
  if (e['max']) return `Must be ≤ ${e['max'].max}`;
  if (e['pattern']) return 'Invalid format';
  return 'Invalid value';
}

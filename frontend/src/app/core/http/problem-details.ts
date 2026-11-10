import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup, ValidationErrors } from '@angular/forms';
import { ProblemDetails } from '../api/api.models';
import { tNow } from '../i18n/translate-now';

/** What a failed call's JSON body can carry: the RFC 7807 members and the envelope's message. */
export type ErrorBody = ProblemDetails & { message?: string };

/** The body of a failed call when the server sent an object (the error envelope), else null. */
export function errorBody(err: HttpErrorResponse): ErrorBody | null {
  return err.error && typeof err.error === 'object' ? (err.error as ErrorBody) : null;
}

/** A failed call as ProblemDetails (status always set), or null when it was not an HTTP error. */
export function asProblem(err: unknown): ProblemDetails | null {
  if (err instanceof HttpErrorResponse && err.error && typeof err.error === 'object') {
    const problem: ProblemDetails = { status: err.status, ...(err.error as ProblemDetails) };
    // A server failure's detail is for developers (a stack trace in development), so it never reaches the screen.
    return err.status >= 500 ? { ...problem, detail: undefined } : problem;
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

/** The error objects Angular's built-in validators set. */
interface LengthError { requiredLength: number; }
interface MinError { min: number; }
interface MaxError { max: number; }

/** Human message for the first error on a control (used by nz-form-control [nzErrorTip]). */
export function controlError(control: AbstractControl | null): string {
  const e: ValidationErrors | null | undefined = control?.errors;
  if (!e) return '';
  if (e['server']) return e['server'] as string;
  if (e['required']) return tNow('ui.valid.required', 'Required');
  if (e['maxlength']) return tNow('ui.valid.maxLength', 'Max {n} characters', { n: (e['maxlength'] as LengthError).requiredLength });
  if (e['minlength']) return tNow('ui.valid.minLength', 'Min {n} characters', { n: (e['minlength'] as LengthError).requiredLength });
  if (e['min']) return tNow('ui.valid.min', 'Must be ≥ {n}', { n: (e['min'] as MinError).min });
  if (e['max']) return tNow('ui.valid.max', 'Must be ≤ {n}', { n: (e['max'] as MaxError).max });
  if (e['pattern']) return tNow('ui.valid.pattern', 'Invalid format');
  return tNow('ui.valid.invalid', 'Invalid value');
}

import { AbstractControl, FormArray, FormGroup } from '@angular/forms';

export interface RevealErrorsOptions {
  /** Also mark every control (nested ones too) as touched first, so untouched fields show their errors. */
  touchAll?: boolean;
  /** Re-validate each control on its own, without re-running the parent's validation. */
  onlySelf?: boolean;
}

/**
 * Makes the form show its validation errors after a submit attempt: each top-level control is marked dirty and re-validated.
 * Replaces the `Object.values(form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); })` copies.
 */
export function revealErrors(form: FormGroup | FormArray, options: RevealErrorsOptions = {}): void {
  if (options.touchAll) form.markAllAsTouched();
  const controls: AbstractControl[] = Object.values(form.controls);
  for (const control of controls) {
    control.markAsDirty();
    if (options.onlySelf) control.updateValueAndValidity({ onlySelf: true });
    else control.updateValueAndValidity();
  }
}

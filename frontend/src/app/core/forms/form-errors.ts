import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { Subject } from 'rxjs';

/** The forms a user just tried to submit while something was wrong. The summary at the top of each form listens to this. */
export const formErrorAttempts = new Subject<AbstractControl>();

/**
 * The one thing a form does when submit finds a problem: every field shows its error and the form lists them in its summary.
 * Pass the form, or only the part of it the user can see on this step.
 */
export function showFormErrors(control: AbstractControl): void {
  markForDisplay(control);
  let root = control;
  while (root.parent) root = root.parent;
  formErrorAttempts.next(root);
}

function markForDisplay(control: AbstractControl): void {
  if (control instanceof FormGroup || control instanceof FormArray) {
    for (const child of Object.values(control.controls) as AbstractControl[]) markForDisplay(child);
    return;
  }
  control.markAsDirty();
  control.markAsTouched();
  control.updateValueAndValidity();
}

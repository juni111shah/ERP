import { FormGroup } from '@angular/forms';

/**
 * Enables or disables the named controls of a form without emitting value or status events,
 * e.g. the key fields of a record that cannot change once it exists. A disabled control is not validated.
 */
export function setControlsEnabled(form: FormGroup, keys: readonly string[], enabled: boolean): void {
  for (const key of keys) {
    const control = form.get(key)!;
    if (enabled) control.enable({ emitEvent: false });
    else control.disable({ emitEvent: false });
  }
}

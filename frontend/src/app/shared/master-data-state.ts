import { signal } from '@angular/core';
import { Observable } from 'rxjs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ProblemDetails } from '../core/api/api.models';
import { asProblem } from '../core/http/problem-details';
import { currentLang, phraseNow, tNow } from '../core/i18n/translate-now';

export interface MasterDataDeps {
  /** create (id = null) or update; returns the saved record */
  save: (id: string | null, body: unknown) => Observable<unknown>;
  /** refresh the list / detail after a save */
  reload: () => void;
  message: NzMessageService;
  /** shown in the success toast, e.g. "Unit" */
  entityName: string;
}

/**
 * State and save flow shared by every master-data screen that edits records in a <erp-record-drawer>:
 * open/close, the record being edited, saving flag, success toast and server validation errors.
 */
export class MasterDataState<T extends { id: string }> {
  readonly open = signal(false);
  readonly editing = signal<T | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  constructor(private readonly deps: MasterDataDeps) {}

  create(): void { this.show(null); }
  edit(record: T): void { this.show(record); }
  close(): void { this.open.set(false); }

  submit(payload: Record<string, unknown>): void {
    this.saving.set(true);
    this.errors.set(null);
    this.deps.save(this.editing()?.id ?? null, payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.open.set(false);
        this.deps.message.success(tNow('ui.msg.entitySaved', '{entity} saved', { entity: this.entityLabel(false) }));
        this.deps.reload();
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        const hasFieldErrors = !!problem?.errors && Object.keys(problem.errors).length > 0;
        if (!hasFieldErrors) this.deps.message.error(problem?.detail ?? problem?.title ?? tNow('ui.msg.entitySaveFailed', 'Could not save the {entity}', { entity: this.entityLabel(true) }));
      },
    });
  }

  private show(record: T | null): void {
    this.errors.set(null);
    this.editing.set(record);
    this.open.set(true);
  }

  /** The entity name for a toast: unchanged in English (lower-cased mid-sentence), the Arabic noun (ui.entity.*) otherwise. */
  private entityLabel(midSentence: boolean): string {
    const name = this.deps.entityName;
    if (currentLang() !== 'ar') return midSentence ? name.toLowerCase() : name;
    const key = 'ui.entity.' + name.toLowerCase().replace(/\s+(\w)/g, (_, c: string) => c.toUpperCase());
    return tNow(key, phraseNow(name));
  }
}

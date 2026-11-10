import { Observable } from 'rxjs';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';

export interface DeleteWithConfirmOptions {
  /** the question, e.g. "Delete this checklist?" */
  title: string;
  /** the delete call */
  remove: () => Observable<unknown>;
  /** the success toast says doneText('deleted', entity, name) */
  entity: string;
  name?: string | null;
  /** shown when the server sends no message */
  failure: string;
  /** runs after a successful delete (close the drawer, reload the list) */
  done: () => void;
}

/**
 * Asks "Delete ...?" (a red Delete button) and deletes on OK: a success toast and `done()`, or the server's message (else
 * `failure`). The modal stays busy until the call returns.
 */
export function deleteWithConfirm(modal: NzModalService, message: NzMessageService, options: DeleteWithConfirmOptions): void {
  modal.confirm({
    nzTitle: options.title, nzOkText: 'Delete', nzOkDanger: true,
    nzOnOk: () => new Promise<void>((resolve) => options.remove().subscribe({
      next: () => { message.success(doneText('deleted', options.entity, options.name)); options.done(); resolve(); },
      error: (e) => { message.error(problemMessage(e, options.failure)); resolve(); },
    })),
  });
}

import { Injectable } from '@angular/core';
import { downloadAttachment } from '@shared/components/attachments/attachments.component';
import { CrudApiService } from './crud-api.service';

/** A tracked document in the workspace's vault: a file attached to a record, with an optional expiry date. */
export interface VaultRow { id: string; ownerType: string; ownerId: string; fileName: string; category?: string; description?: string; expiresOn?: string; createdAt: string; }

/** The document vault: every tracked attachment in the workspace (read-only list), and the download of one file. */
@Injectable({ providedIn: 'root' })
export class VaultApi extends CrudApiService<VaultRow, never> {
  protected readonly resource = 'attachments/vault';

  /** Fetches the file with the sign-in header and hands it to the browser as a download. */
  download(row: VaultRow): void { downloadAttachment(this.http, this.baseUrl, row); }
}

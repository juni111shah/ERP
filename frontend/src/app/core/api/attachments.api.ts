import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** A document attached to a record. */
export interface AttachmentItem { id: string; fileName: string; contentType: string; sizeBytes: number; createdAt: string; category?: string; description?: string; expiresOn?: string; }

/** Documents attached to any record (/api/v1/attachments): list per owner, upload, download and delete. */
@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/attachments`;

  list(ownerType: string, ownerId: string): Observable<AttachmentItem[]> { return this.http.get<AttachmentItem[]>(this.url, { params: { ownerType, ownerId } }); }
  /** The form holds ownerType, ownerId and file, plus the optional category and expiresOn. */
  upload(form: FormData): Observable<unknown> { return this.http.post(this.url, form); }
  download(id: string): Observable<Blob> { return this.http.get(`${this.url}/${id}/download`, { responseType: 'blob' }); }
  delete(id: string): Observable<unknown> { return this.http.delete(`${this.url}/${id}`); }
}

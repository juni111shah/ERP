import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** Something that can be bulk-loaded from CSV, and the columns its file takes. */
export interface ImportDef { key: string; label: string; columns: { name: string; required: boolean; example: string; hint?: string }[]; }
/** What a check (dry run) or an import did with a file. */
export interface ImportResult { total: number; created: number; errors: { row: number; message: string }[]; dryRun: boolean; }

/** CSV imports: what can be imported, the template file of each, and the check / import of an uploaded file. */
@Injectable({ providedIn: 'root' })
export class ImportsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/imports`;

  definitions(): Observable<ImportDef[]> { return this.http.get<ImportDef[]>(this.url); }
  /** The CSV template (header row and an example) for one import. */
  template(key: string): Observable<string> { return this.http.get(`${this.url}/${key}/template`, { responseType: 'text' }); }
  /** Checks the file (`dryRun`) or imports it. */
  run(key: string, file: File, dryRun: boolean): Observable<ImportResult> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImportResult>(`${this.url}/${key}`, form, { params: { dryRun } });
  }
}

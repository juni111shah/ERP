import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** A module as the workspace sees it: what it is, what it needs, and whether it is switched on here. */
export interface WorkspaceModule {
  key: string; name: string; description: string; category: string; version: string; isCore: boolean; requires: string[]; uiRoute: string;
  state: 'enabled' | 'disabled' | 'unavailable';
}

/** The workspace's modules: the list, and switching one on or off (its data is kept). */
@Injectable({ providedIn: 'root' })
export class ModulesApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/modules`;

  list(): Observable<WorkspaceModule[]> { return this.http.get<WorkspaceModule[]>(this.url); }
  setState(key: string, action: 'enable' | 'disable'): Observable<unknown> { return this.http.post(`${this.url}/${key}/${action}`, {}); }
}

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** One link of the server-driven sidebar menu (MenuItemDefinition). The route may carry a query string. */
export interface NavItem { id: string; label: string; route: string; icon?: string; module: string; }
/** One category of the sidebar menu, with the links this person may open. */
export interface NavGroup { id: string; label: string; order: number; items: NavItem[]; }

/** The app menu for the signed-in person and workspace (/api/v1/navigation). */
@Injectable({ providedIn: 'root' })
export class NavigationApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  menu(): Observable<NavGroup[]> { return this.http.get<NavGroup[]>(`${this.base}/navigation/menu`); }
}

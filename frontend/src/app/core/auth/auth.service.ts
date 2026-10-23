import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { MeResponse, SessionStore, Workspace } from '../session/session.store';

export interface SessionResponse { accessToken: string; expiresIn: number; workspace: Workspace; workspaces: Workspace[]; persistent: boolean; }
export interface LoginResponse extends Partial<SessionResponse> { requiresWorkspaceSelection?: boolean; selectionToken?: string; workspaces: Workspace[]; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private refreshInFlight: Observable<string | null> | null = null;

  login(email: string, password: string, rememberMe: boolean, twoFactorCode?: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, { email, password, rememberMe, twoFactorCode }).pipe(
      switchMap((r) => (r.accessToken ? this.startSession(r as SessionResponse).pipe(map(() => r)) : of(r))),
    );
  }

  selectWorkspace(selectionToken: string, workspaceId: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${this.base}/auth/select-workspace`, { selectionToken, workspaceId })
      .pipe(switchMap((s) => this.startSession(s).pipe(map(() => s))));
  }

  switchWorkspace(workspaceId: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${this.base}/auth/switch-workspace`, { workspaceId }).pipe(
      tap(() => this.session.setEntity(null)),
      switchMap((s) => this.startSession(s).pipe(map(() => s))),
    );
  }

  /** Single-flight silent refresh: concurrent 401s share one /auth/refresh call. */
  refresh(): Observable<string | null> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.http.post<SessionResponse>(`${this.base}/auth/refresh`, {}).pipe(
        switchMap((s) => this.startSession(s).pipe(map(() => s.accessToken))),
        catchError(() => { this.session.clear(); return of(null); }),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay(1),
      );
    }
    return this.refreshInFlight;
  }

  /** App start: restore the session from the refresh cookie so a hard reload keeps the user signed in. */
  restore(): Observable<unknown> {
    return this.refresh();
  }

  logout(): void {
    this.http.post(`${this.base}/auth/logout`, {}).pipe(catchError(() => of(null))).subscribe(() => {
      this.session.clear();
      void this.router.navigateByUrl('/auth/login');
    });
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post(`${this.base}/auth/forgot-password`, { email });
  }

  resetPassword(token: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${this.base}/auth/reset-password`, { token, newPassword });
  }

  loadMe(): Observable<MeResponse> {
    return this.http.get<MeResponse>(`${this.base}/me`).pipe(tap((me) => {
      this.session.me.set(me);
      const current = this.session.currentEntityId();
      if (current && !me.legalEntities.some((e) => e.id === current)) this.session.setEntity(null);
      if (!me.allLegalEntities && !this.session.currentEntityId() && me.legalEntities.length) this.session.setEntity(me.legalEntities[0].id);
    }));
  }

  private startSession(s: SessionResponse): Observable<MeResponse> {
    this.session.accessToken.set(s.accessToken);
    return this.loadMe();
  }
}

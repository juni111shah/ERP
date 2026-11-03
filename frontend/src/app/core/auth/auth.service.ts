import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { MeResponse, SessionStore, Workspace } from '../session/session.store';

export interface SessionResponse { accessToken: string; expiresIn: number; workspace: Workspace; workspaces: Workspace[]; persistent: boolean; }
export interface LoginResponse extends Partial<SessionResponse> {
  requiresWorkspaceSelection?: boolean; selectionToken?: string; workspaces: Workspace[];
  /** Two-factor still to do: no session yet, only a short-lived challenge for the next step. */
  requiresTwoFactor?: boolean; twoFactorRequired?: boolean; twoFactorSetupRequired?: boolean; challengeToken?: string; challengeExpiresIn?: number;
  /** One-time recovery codes, returned once when a first-sign-in authenticator setup is confirmed. */
  recoveryCodes?: string[];
}

/** First-sign-in authenticator setup: the QR image of the otpauth:// link and the key for typing in by hand. */
export interface TwoFactorChallengeSetup { otpAuthUri: string; secret: string; qrCodeDataUri: string; issuer: string; account: string; digits: number; period: number; }

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

  /** Setup step of a login challenge: creates the authenticator key and returns the QR / manual key. */
  beginTwoFactorSetup(challengeToken: string): Observable<TwoFactorChallengeSetup> {
    return this.http.post<TwoFactorChallengeSetup>(`${this.base}/auth/login/2fa/setup`, { challengeToken });
  }

  /** Confirms a login challenge with a code (first code of a new setup, or a code / recovery code) and starts the session. */
  verifyTwoFactor(challengeToken: string, code: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login/2fa/verify`, { challengeToken, code }).pipe(
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

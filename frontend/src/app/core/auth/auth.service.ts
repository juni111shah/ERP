import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { AuthSessionApi, LoginResponse, SessionResponse, TwoFactorChallengeSetup } from '../api/auth-session.api';
import { MeResponse, SessionStore } from '../session/session.store';

export type { LoginResponse, SessionResponse, TwoFactorChallengeSetup } from '../api/auth-session.api';

/** Signs people in and out and keeps the session store in step: the access token, the signed-in person and the legal entity. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authApi = inject(AuthSessionApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private refreshInFlight: Observable<string | null> | null = null;

  login(email: string, password: string, rememberMe: boolean, twoFactorCode?: string): Observable<LoginResponse> {
    return this.authApi.login({ email, password, rememberMe, twoFactorCode }).pipe(switchMap((r) => this.startSessionIfIssued(r)));
  }

  /** Setup step of a login challenge: creates the authenticator key and returns the QR / manual key. */
  beginTwoFactorSetup(challengeToken: string): Observable<TwoFactorChallengeSetup> {
    return this.authApi.beginTwoFactorSetup(challengeToken);
  }

  /** Confirms a login challenge with a code (first code of a new setup, or a code / recovery code) and starts the session. */
  verifyTwoFactor(challengeToken: string, code: string): Observable<LoginResponse> {
    return this.authApi.verifyTwoFactor(challengeToken, code).pipe(switchMap((r) => this.startSessionIfIssued(r)));
  }

  selectWorkspace(selectionToken: string, workspaceId: string): Observable<SessionResponse> {
    return this.authApi.selectWorkspace(selectionToken, workspaceId).pipe(switchMap((s) => this.startSession(s).pipe(map(() => s))));
  }

  switchWorkspace(workspaceId: string): Observable<SessionResponse> {
    return this.authApi.switchWorkspace(workspaceId).pipe(
      tap(() => this.session.setEntity(null)),
      switchMap((s) => this.startSession(s).pipe(map(() => s))),
    );
  }

  /** Single-flight silent refresh: concurrent 401s share one /auth/refresh call. */
  refresh(): Observable<string | null> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.authApi.refresh().pipe(
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

  /** Ends the session on the server (a failure is ignored), clears it here and goes to the sign-in page. */
  logout(): void {
    this.authApi.logout().pipe(catchError(() => of(null))).subscribe(() => {
      this.session.clear();
      void this.router.navigateByUrl('/auth/login');
    });
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.authApi.forgotPassword(email);
  }

  resetPassword(token: string, newPassword: string): Observable<unknown> {
    return this.authApi.resetPassword(token, newPassword);
  }

  /** Loads the signed-in person and keeps the selected legal entity valid for them. */
  loadMe(): Observable<MeResponse> {
    return this.authApi.me().pipe(tap((me) => {
      this.session.me.set(me);
      this.keepEntityValid(me);
    }));
  }

  /** A login answer that already carries a token starts the session; one that asks for another step comes back as it is. */
  private startSessionIfIssued(r: LoginResponse): Observable<LoginResponse> {
    return r.accessToken ? this.startSession(r as SessionResponse).pipe(map(() => r)) : of(r);
  }

  private startSession(s: SessionResponse): Observable<MeResponse> {
    this.session.accessToken.set(s.accessToken);
    return this.loadMe();
  }

  /** Drops a legal entity the person may no longer use; someone limited to some entities starts on the first of them. */
  private keepEntityValid(me: MeResponse): void {
    const current = this.session.currentEntityId();
    if (current && !me.legalEntities.some((e) => e.id === current)) this.session.setEntity(null);
    if (!me.allLegalEntities && !this.session.currentEntityId() && me.legalEntities.length) this.session.setEntity(me.legalEntities[0].id);
  }
}

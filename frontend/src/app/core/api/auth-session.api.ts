import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MeResponse, Workspace } from '../session/session.store';
import { API_BASE_URL } from './api-config';

/** A started session: the in-memory access token and the workspace it is for. */
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

/** Sign-in, workspace selection, token refresh, password reset and the signed-in person (/api/v1/auth, /api/v1/me). */
@Injectable({ providedIn: 'root' })
export class AuthSessionApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  login(body: { email: string; password: string; rememberMe: boolean; twoFactorCode?: string }): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login`, body);
  }
  beginTwoFactorSetup(challengeToken: string): Observable<TwoFactorChallengeSetup> {
    return this.http.post<TwoFactorChallengeSetup>(`${this.base}/auth/login/2fa/setup`, { challengeToken });
  }
  verifyTwoFactor(challengeToken: string, code: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/auth/login/2fa/verify`, { challengeToken, code });
  }
  selectWorkspace(selectionToken: string, workspaceId: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${this.base}/auth/select-workspace`, { selectionToken, workspaceId });
  }
  switchWorkspace(workspaceId: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${this.base}/auth/switch-workspace`, { workspaceId });
  }
  refresh(): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${this.base}/auth/refresh`, {});
  }
  logout(): Observable<unknown> {
    return this.http.post(`${this.base}/auth/logout`, {});
  }
  forgotPassword(email: string): Observable<unknown> {
    return this.http.post(`${this.base}/auth/forgot-password`, { email });
  }
  resetPassword(token: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${this.base}/auth/reset-password`, { token, newPassword });
  }
  me(): Observable<MeResponse> {
    return this.http.get<MeResponse>(`${this.base}/me`);
  }
}

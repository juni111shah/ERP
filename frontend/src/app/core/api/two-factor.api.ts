import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** Whether the signed-in person has two-factor authentication on. */
export interface TwoFactorStatus { enabled: boolean; }
/** A new authenticator secret, and the otpauth:// address a QR code is drawn from. */
export interface TwoFactorSetup { secret: string; otpAuthUri: string; }
/** The one-time recovery codes given when two-factor authentication is switched on. */
export interface TwoFactorEnabled { recoveryCodes: string[]; }

/** The signed-in person's own two-factor authentication (/api/v1/auth/2fa): status, set up, switch on with the first code, switch off. */
@Injectable({ providedIn: 'root' })
export class TwoFactorApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  status(): Observable<TwoFactorStatus> {
    return this.http.get<TwoFactorStatus>(`${this.base}/auth/2fa`);
  }

  setup(): Observable<TwoFactorSetup> {
    return this.http.post<TwoFactorSetup>(`${this.base}/auth/2fa/setup`, {});
  }

  enable(code: string): Observable<TwoFactorEnabled> {
    return this.http.post<TwoFactorEnabled>(`${this.base}/auth/2fa/enable`, { code });
  }

  disable(password: string, code: string): Observable<unknown> {
    return this.http.post(`${this.base}/auth/2fa/disable`, { password, code });
  }
}

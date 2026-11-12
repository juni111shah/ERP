import { WritableSignal } from '@angular/core';
import { ProblemDetails } from '@core/api/api.models';
import { LoginResponse } from '@core/auth/auth.service';

/** setup: first sign-in without an authenticator; verify: a code from the app (or a recovery code). */
export type TwoFactorMode = 'setup' | 'verify';

/** The two-factor step a sign-in answer asks for. */
export interface TwoFactorChallenge { mode: TwoFactorMode; token: string; }

/** The two-factor step to show after a correct password: first-time setup, or a code for a returning user; null when none. */
export function twoFactorChallengeOf(r: LoginResponse): TwoFactorChallenge | null {
  if (r.twoFactorSetupRequired && r.challengeToken) return { mode: 'setup', token: r.challengeToken };
  if ((r.twoFactorRequired || r.requiresTwoFactor) && r.challengeToken) return { mode: 'verify', token: r.challengeToken };
  return null;
}

/** How long a rate-limited sign-in must wait, from the server's answer (a minute when it does not say). */
export function retryAfterSeconds(p: ProblemDetails): number {
  return Number((p as { retryAfterSeconds?: number }).retryAfterSeconds ?? 60);
}

/** Why a sign-in failed, in words (other than a rate limit). */
export function signInFailureText(p: ProblemDetails | null): string {
  return p?.status === 0 ? 'Cannot reach the server. Check your connection.' : (p?.detail ?? 'Incorrect email or password.');
}

/** Counts `remaining` down to zero, one step a second. */
export function startCountdown(remaining: WritableSignal<number>, seconds: number): void {
  remaining.set(seconds);
  const t = setInterval(() => { remaining.update((s) => s - 1); if (remaining() <= 0) clearInterval(t); }, 1000);
}

/** Why a two-factor code was refused, in words; null when the challenge expired and the person must sign in again. */
export function twoFactorFailureText(p: ProblemDetails | null, t: (key: string, fallback: string) => string): string | null {
  if (p?.status === 401 && /expired|sign in again/i.test(p.detail ?? '')) return null;
  if (p?.status === 401) return t('twofa.error.wrong', "That code didn't work. Check the time on your phone and enter the newest code.");
  if (p?.status === 423) return t('twofa.error.locked', 'Too many wrong codes. Your account is locked for a few minutes.');
  if (p?.status === 429) return t('twofa.error.tooMany', 'Too many attempts. Please wait a minute and try again.');
  if (p?.status === 0) return t('twofa.error.offline', 'Cannot reach the server. Check your connection.');
  return p?.detail ?? t('twofa.error.generic', 'Something went wrong. Please try again.');
}

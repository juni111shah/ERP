import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../session/session.store';

/** Changes arrive in bursts (a module switch can also add roles): one refresh covers everything within this window. */
const COALESCE_MS = 300;

/**
 * Brings this tab in line with what the person may do now, without a reload or a new sign-in: refreshes the session (new token and
 * /me, so the permission set, the sidebar menu and every *erpHasPermission re-evaluate), then runs the route guards again for the open
 * page. A page the person can no longer open is left for their home page, with a short message (see the access guards).
 */
@Injectable({ providedIn: 'root' })
export class AccessSync {
  private readonly auth = inject(AuthService);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private running = false;
  private again = false;
  /** Asks for a refresh; several calls close together make one. */
  refresh(_reason = 'access-changed'): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.timer = null; this.run(); }, COALESCE_MS);
  }

  private run(): void {
    if (this.running) { this.again = true; return; }
    if (!this.session.isAuthenticated()) return;
    this.running = true;
    this.auth.refresh().subscribe({
      next: (token) => {
        // No token: the session has ended (for example the person was deactivated), so they sign in again.
        if (!token) { void this.router.navigateByUrl('/auth/login'); return; }
        void this.revalidateOpenPage();
      },
      complete: () => this.finish(),
      error: () => this.finish(),
    });
  }

  private finish(): void {
    this.running = false;
    if (this.again) { this.again = false; this.run(); }
  }

  /** Navigates to the same address again: the canMatch guards run once more, and a page that is no longer allowed redirects home. */
  async revalidateOpenPage(): Promise<void> {
    const url = this.router.url;
    if (url.startsWith('/auth') || url.startsWith('/portal')) return;
    this.session.rechecking.set(true);
    try { await this.router.navigateByUrl(url, { onSameUrlNavigation: 'reload', replaceUrl: true }); }
    catch { /* a failed navigation leaves the page as it is */ }
    finally { this.session.rechecking.set(false); }
  }
}

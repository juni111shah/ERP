import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { SessionStore } from '@core/session/session.store';

/** Moves the signed-in person to another workspace or legal entity, from the shell header or the blocked-workspace page. */
@Injectable({ providedIn: 'root' })
export class WorkspaceSwitcher {
  private readonly auth = inject(AuthService);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  /** Signs in to another workspace of the person and opens its home page. */
  switchWorkspace(id: string): void {
    this.auth.switchWorkspace(id).subscribe(() => void this.router.navigateByUrl('/'));
  }

  /** Works in another legal entity (null: all of them) and reloads the current screen, so lists re-query in the new scope. */
  selectEntity(id: string | null): void {
    this.session.setEntity(id);
    const url = this.router.url;
    void this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => this.router.navigateByUrl(url));
  }
}

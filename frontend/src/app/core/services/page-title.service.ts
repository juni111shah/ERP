import { inject, Injectable, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { I18nService } from '../i18n/i18n.service';
import { partiesTitle, RouteTitle, routeTitle, TitleTranslator } from './page-titles';

/** A small tag next to the page title (a record's status, say). */
export interface PageBadge {
  text: string;
  color?: string;
}

/** The title bar's state: set from the address on every navigation, or by a page that knows a better title (a record's number). */
@Injectable({
  providedIn: 'root',
})
export class PageTitleService {
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);
  private readonly t: TitleTranslator = (key, fallback) => this.i18n.t(key, fallback);

  readonly title = signal<string>('Dashboard');
  readonly subtitle = signal<string | null>(null);
  readonly badge = signal<PageBadge | null>(null);
  readonly module = signal<string | null>(null);

  private customTitleUrl: string | null = null;

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        if (this.customTitleUrl !== e.urlAfterRedirects) {
          this.resolveFromRoute(e.urlAfterRedirects);
        }
      });
  }

  /** Explicitly set or customize the page title (e.g. for dynamic record numbers) */
  setTitle(title: string, subtitle?: string | null, badge?: PageBadge | null, module?: string | null): void {
    this.title.set(title);
    this.subtitle.set(subtitle ?? null);
    this.badge.set(badge ?? null);
    if (module !== undefined) {
      this.module.set(module ?? null);
    }
    this.customTitleUrl = this.router.url;
  }

  private resolveFromRoute(url: string): void {
    const cleanUrl = url.split('?')[0].split('#')[0];
    if (cleanUrl === '/parties') {
      const query = new URLSearchParams(url.includes('?') ? url.split('?')[1] : '');
      this.show(partiesTitle(query.get('type'), this.t));
      return;
    }
    const matched = routeTitle(cleanUrl, this.t);
    if (matched) this.show(matched);
  }

  private show(route: RouteTitle): void {
    this.title.set(route.title);
    this.subtitle.set(route.subtitle ?? null);
    this.badge.set(null);
    this.module.set(route.module ?? null);
  }
}

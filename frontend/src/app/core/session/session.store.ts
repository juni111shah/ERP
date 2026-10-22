import { Injectable, computed, signal } from '@angular/core';

export interface Workspace { id: string; code: string; name: string; industry?: string; industryCode?: string; }
export interface LegalEntityOption { id: string; code: string; name: string; baseCurrency: string; }
export interface MeResponse {
  user: { id: string; email: string; fullName: string; mustChangePassword: boolean };
  workspace: Workspace & { countryCode: string; baseCurrency: string; timeZone: string; defaultLocale: string };
  workspaces: Workspace[];
  permissions: string[];
  legalEntities: LegalEntityOption[];
  allLegalEntities: boolean;
  enabledModules: { key: string; uiRoute: string }[];
}

const ENTITY_KEY = 'erp.entity';

/** Global session state (signals). The access token lives in memory only; the refresh token is an HttpOnly cookie. */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  readonly accessToken = signal<string | null>(null);
  readonly me = signal<MeResponse | null>(null);
  readonly permissions = computed<ReadonlySet<string>>(() => new Set(this.me()?.permissions ?? []));
  /** null = "All legal entities" */
  readonly currentEntityId = signal<string | null>(sessionStorage.getItem(ENTITY_KEY));
  readonly enabledModules = computed<ReadonlySet<string>>(() => new Set(['core', ...(this.me()?.enabledModules.map((m) => m.key) ?? [])]));
  readonly isAuthenticated = computed(() => !!this.accessToken());
  readonly isOrgAdmin = computed(() => this.permissions().has('*'));

  hasPermission(permission: string): boolean {
    return this.permissions().has('*') || this.permissions().has(permission);
  }

  isModuleEnabled(moduleKey: string): boolean {
    return moduleKey === 'core' || this.enabledModules().has(moduleKey);
  }

  setEntity(id: string | null): void {
    this.currentEntityId.set(id);
    if (id) sessionStorage.setItem(ENTITY_KEY, id);
    else sessionStorage.removeItem(ENTITY_KEY);
  }

  clear(): void {
    this.accessToken.set(null);
    this.me.set(null);
    this.setEntity(null);
  }
}

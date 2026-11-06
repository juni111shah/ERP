import { Injectable, computed, signal } from '@angular/core';

export interface Workspace { id: string; code: string; name: string; industry?: string; industryCode?: string; }
export interface LegalEntityOption { id: string; code: string; name: string; baseCurrency: string; }
/** How the current workspace stands against its plan: drives the banner, the blocked page and the billing screen. */
export interface SubscriptionSummary {
  hasSubscription: boolean;
  status: 'trialing' | 'active' | 'pastDue' | 'expired' | 'suspended' | 'cancelled' | null;
  access: 'full' | 'readOnly' | 'blocked';
  planName: string | null;
  trialEndsOn: string | null;
  trialDaysLeft: number | null;
  currentPeriodEnd: string | null;
  lapsedOn: string | null;
  isComplimentary: boolean;
  suspendedReason: string | null;
}
export interface MeResponse {
  user: { id: string; email: string; fullName: string; mustChangePassword: boolean };
  workspace: Workspace & { countryCode: string; baseCurrency: string; timeZone: string; defaultLocale: string };
  workspaces: Workspace[];
  permissions: string[];
  legalEntities: LegalEntityOption[];
  allLegalEntities: boolean;
  enabledModules: { key: string; uiRoute: string }[];
  /** Member of the team that runs the platform: may open the console at /platform. */
  platformAdmin?: boolean;
  /** This workspace is the platform's own, not a client. */
  isInternalWorkspace?: boolean;
  subscription?: SubscriptionSummary;
}

const ENTITY_KEY = 'erp.entity';
const STAY_IN_WORKSPACE_KEY = 'erp.stayInWorkspace';

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
  /** A founder chose "Back to workspace": the root route shows the workspace in this tab rather than the console. */
  readonly stayInWorkspace = signal(sessionStorage.getItem(STAY_IN_WORKSPACE_KEY) === '1');

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

  setStayInWorkspace(stay: boolean): void {
    this.stayInWorkspace.set(stay);
    if (stay) sessionStorage.setItem(STAY_IN_WORKSPACE_KEY, '1');
    else sessionStorage.removeItem(STAY_IN_WORKSPACE_KEY);
  }

  clear(): void {
    this.accessToken.set(null);
    this.me.set(null);
    this.setEntity(null);
    this.setStayInWorkspace(false);
  }
}

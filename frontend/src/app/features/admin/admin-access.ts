import { PermissionDefinition, UserSummary } from '@core/api/admin.api';

/** Permissions grouped under "module · resource", in catalogue order. */
export function groupPermissions(permissions: PermissionDefinition[]): { title: string; items: PermissionDefinition[] }[] {
  const map = new Map<string, PermissionDefinition[]>();
  for (const p of permissions) map.set(`${p.module} · ${p.resource}`, [...(map.get(`${p.module} · ${p.resource}`) ?? []), p]);
  return [...map].map(([title, items]) => ({ title, items }));
}

/** The counts on the users page: active users, administrators, users who have signed in, and everyone. */
export interface UserStats { active: number; admins: number; recent: number; total: number; }

/** Counts the users for the stat cards; an administrator is anyone with a role whose name contains "admin". */
export function userStats(list: UserSummary[]): UserStats {
  const active = list.filter((u) => u.isActive).length;
  const admins = list.filter((u) => u.roles.some((r) => r.toLowerCase().includes('admin'))).length;
  const recent = list.filter((u) => !!u.lastSignInAt).length;
  return { active, admins, recent, total: list.length };
}

/** Up to two initials of a name for the avatar ("U" when there is no name). */
export function userInitials(name?: string): string {
  return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'U';
}

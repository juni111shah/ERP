/**
 * Rules of the app sidebar that need no DOM: each group's icon, the group holding the page, the open group (and where it is
 * remembered), the menu search, menu links and keyboard moves.
 */
import { NavGroup } from '@core/api/navigation.api';
import { KNOWN_ICONS } from '../icons';

export type { NavGroup, NavItem } from '@core/api/navigation.api';

/** Shown for a group this map does not know yet, for example a newly installed module. */
export const FALLBACK_GROUP_ICON = 'appstore';

/** Menu group id (MenuItemDefinition.GroupId) to icon. Every icon must be registered in icons.ts. */
export const GROUP_ICONS: ReadonlyMap<string, string> = new Map([
  ['workspace', 'dashboard'],
  ['sales', 'rise'],
  ['purchases', 'shopping-cart'],
  ['finance', 'account-book'],
  ['masterdata', 'database'],
  ['setup', 'setting'],
  ['realestate', 'home'],
  ['crm', 'contacts'],
  ['services', 'customer-service'],
  ['inventory', 'inbox'],
  ['manufacturing', 'tool'],
  ['distribution', 'car'],
  ['retail', 'shopping'],
  ['pos', 'shop'],
  ['hr', 'idcard'],
  ['projects', 'project'],
  ['projects.money', 'field-time'],
]);

export function groupIcon(groupId: string): string {
  return GROUP_ICONS.get(groupId) ?? FALLBACK_GROUP_ICON;
}

/**
 * The group whose item best matches the URL: the longest route path wins, then the most query values, then the first group.
 * Several groups can link to one page with different filters (/parties?type=customer), so the query counts.
 */
export function currentGroupId(groups: readonly NavGroup[], url: string): string | null {
  const [path, query = ''] = url.split('#')[0].split('?');
  const params = new URLSearchParams(query);
  let best: string | null = null;
  let bestScore = -1;
  for (const group of groups) {
    for (const item of group.items) {
      const [route, routeQuery = ''] = (item.route ?? '').split('?');
      const routePath = route || '/';
      const inPath = routePath === '/' ? path === '/' : path === routePath || path.startsWith(routePath + '/');
      const wanted = [...new URLSearchParams(routeQuery)];
      if (!inPath || !wanted.every(([key, value]) => params.get(key) === value)) continue;
      const score = routePath.length * 100 + wanted.length;
      if (score > bestScore) { best = group.id; bestScore = score; }
    }
  }
  return best;
}

/**
 * The one open group after the page or the menu changes. The group holding the page wins; otherwise the previous choice stays
 * while it still exists (null: the user closed them all; undefined: no choice yet, so the first group opens).
 */
export function resolveOpenGroup(groups: readonly NavGroup[], url: string, previous: string | null | undefined): string | null | undefined {
  if (!groups.length) return previous; // menu not loaded yet
  const current = currentGroupId(groups, url);
  if (current) return current;
  if (previous === null) return null;
  return groups.some((g) => g.id === previous) ? previous : groups[0].id;
}

/** Index to focus after a key press in a list of links, or null when the key does not move focus. */
export function nextFocusIndex(key: string, index: number, count: number): number | null {
  if (count === 0) return null;
  switch (key) {
    case 'ArrowDown': return (index + 1) % count;
    case 'ArrowUp': return index <= 0 ? count - 1 : index - 1;
    case 'Home': return 0;
    case 'End': return count - 1;
    default: return null;
  }
}

/** How far to scroll so a block fits in the view; when it is too tall its top stays visible. */
export function scrollDeltaToReveal(viewTop: number, viewBottom: number, blockTop: number, blockBottom: number): number {
  const delta = blockBottom > viewBottom ? blockBottom - viewBottom : 0;
  return blockTop - delta < viewTop ? blockTop - viewTop : delta;
}

/** A menu item's icon when the app registers it (icons.ts), else "appstore". */
export function menuIcon(name?: string): string {
  return name && KNOWN_ICONS.has(name) ? name : 'appstore';
}

/** Menu routes may carry a query string (e.g. /parties?type=customer); routerLink would encode the "?" and miss the route. */
export function menuLinkPath(route: string): string {
  return (route ?? '').split('?')[0] || '/';
}

/** The query values of a menu route, or null when it has none. */
export function menuLinkQuery(route: string): Record<string, string> | null {
  const q = (route ?? '').split('?')[1];
  if (!q) return null;
  const out: Record<string, string> = {};
  new URLSearchParams(q).forEach((v, k) => (out[k] = v));
  return out;
}

/**
 * The menu narrowed to a search term: a group whose label (or translated label) matches keeps all its items, otherwise only the items
 * whose label, translated label or route match stay; groups left empty go. Without a term the menu is returned as it is.
 */
export function filterMenu(groups: NavGroup[], search: string, t: (key: string, fallback: string) => string): NavGroup[] {
  const term = search.trim().toLowerCase();
  if (!term) return groups;
  const has = (text: string | undefined) => (text || '').toLowerCase().includes(term);
  return groups
    .map((g) => {
      const groupMatches = has(g.label) || has(t('menu.group.' + g.id, g.label));
      const items = g.items.filter((item) => groupMatches || has(item.label) || has(t('menu.' + item.id, item.label)) || has(item.route));
      return { ...g, items };
    })
    .filter((g) => g.items.length > 0);
}

const OPEN_GROUP_KEY = 'erp.nav.openGroup';

/** The sidebar group the person last left open, if the browser kept it. */
export function readOpenGroup(): string | undefined {
  try { return localStorage.getItem(OPEN_GROUP_KEY) ?? undefined; } catch { return undefined; }
}

/** Remembers the open sidebar group (null: the person closed them all). */
export function saveOpenGroup(id: string | null): void {
  try {
    if (id) localStorage.setItem(OPEN_GROUP_KEY, id);
    else localStorage.removeItem(OPEN_GROUP_KEY);
  } catch { /* storage unavailable */ }
}

import { LeaseListItem, ListingDto, PropertyDto, UnitListItem } from '@core/api/realestate.api';

/** The KPI cards of the list pages are worked out from the rows on the page; `partial` says the list has more than that. */

export interface PropertyStats { totalUnits: number; totalValue: number; activeCount: number; count: number; partial: boolean; }

export function propertyStats(items: PropertyDto[], total: number): PropertyStats {
  return {
    totalUnits: items.reduce((s, p) => s + (p.totalUnits || 0), 0),
    totalValue: items.reduce((s, p) => s + (p.indicativeValue || 0), 0),
    activeCount: items.filter((p) => p.isActive).length,
    count: items.length,
    partial: total > items.length,
  };
}

export interface UnitStats { occupied: number; vacant: number; potentialRent: number; count: number; partial: boolean; }

export function unitStats(items: UnitListItem[], total: number): UnitStats {
  return {
    occupied: items.filter((u) => u.status === 'occupied').length,
    vacant: items.filter((u) => u.status === 'vacant').length,
    potentialRent: items.reduce((s, u) => s + (u.askingRent || 0), 0),
    count: items.length,
    partial: total > items.length,
  };
}

export interface LeaseStats { active: number; expiringSoon: number; drafts: number; count: number; partial: boolean; }

/** Active leases, those ending within 60 days, and drafts. */
export function leaseStats(items: LeaseListItem[], total: number): LeaseStats {
  return {
    active: items.filter((l) => l.status === 'active').length,
    expiringSoon: items.filter((l) => l.daysToExpiry != null && l.daysToExpiry <= 60 && l.daysToExpiry >= 0).length,
    drafts: items.filter((l) => l.status === 'draft').length,
    count: items.length,
    partial: total > items.length,
  };
}

export interface ListingStats { published: number; rentals: number; totalValue: number; countOnPage: number; }

export function listingStats(items: ListingDto[]): ListingStats {
  return {
    published: items.filter((l) => l.status === 'published').length,
    rentals: items.filter((l) => l.kind === 'rent').length,
    totalValue: items.reduce((sum, l) => sum + (l.price || 0), 0),
    countOnPage: items.length,
  };
}

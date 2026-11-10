import { Injectable, inject, signal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { LeadSummaryDto, OpportunitySummaryDto } from '@core/api/crm.api';
import { PartyDto } from '@core/api/parties.api';

/** A record as the CRM pickers show it. */
export interface NamedOption {
  id: string;
  name: string;
}

/**
 * The legal-entity pick list of the CRM forms.
 * Provided per page (`providers: [CrmLookups]`) so each page loads its own fresh list.
 */
@Injectable()
export class CrmLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);

  readonly entities = signal<NamedOption[]>([]);

  /** Loads the legal entities and puts the first one on `form` as its legal entity. */
  loadEntities(form: FormGroup): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => {
        if (!res.items) return;
        this.entities.set(res.items.map((e) => ({ id: e.id, name: e.name })));
        if (res.items.length > 0) form.patchValue({ legalEntityId: res.items[0].id });
      },
    });
  }
}

/** A lead as a picker option: the person, else the company, else the number. */
export function leadOption(l: LeadSummaryDto): NamedOption {
  return { id: l.id, name: l.fullName || l.companyName || l.leadNumber || l.id };
}

/** A deal as a picker option: its title, else its number. */
export function opportunityOption(o: OpportunitySummaryDto): NamedOption {
  return { id: o.id, name: o.title || o.opportunityNumber || o.id };
}

/** A customer as a picker option. */
export function partyOption(p: PartyDto): NamedOption {
  return { id: p.id, name: p.name || p.id };
}

/** This moment, `days` days from now. */
export function daysFromNow(days: number): Date {
  return new Date(Date.now() + days * 86400000);
}

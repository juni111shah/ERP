import { Injectable } from '@angular/core';
import { CrudApiService } from './crud-api.service';

export interface PartyDto {
  id: string; code: string; name: string; kind: string; roles: string[]; email?: string; phone?: string; taxNumber?: string;
  idNumber?: string; iban?: string; currencyCode?: string; paymentTermsDays?: number; address?: string; isActive: boolean;
  customFields: Record<string, unknown>; partyGroupId?: string | null; creditLimit?: number | null;
}

/** Customers, vendors and other parties (/api/v1/parties). */
@Injectable({ providedIn: 'root' })
export class PartiesApi extends CrudApiService<PartyDto, unknown> {
  protected readonly resource = 'parties';
}

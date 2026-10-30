import { Injectable, computed, inject } from '@angular/core';
import { SessionStore } from '../session/session.store';

@Injectable({ providedIn: 'root' })
export class WorkspaceCurrencyService {
  private readonly session = inject(SessionStore);

  /** Active currency: legal entity currency if selected, else workspace currency, defaulting to USD */
  readonly currency = computed(() => {
    const me = this.session.me();
    const currentEntityId = this.session.currentEntityId();
    if (currentEntityId && me?.legalEntities) {
      const entity = me.legalEntities.find((e) => e.id === currentEntityId);
      if (entity?.baseCurrency) {
        return entity.baseCurrency;
      }
    }
    return me?.workspace?.baseCurrency || 'QAR';
  });

  /** Returns current currency code, e.g. 'USD', 'EUR', 'AED' */
  code(): string {
    return this.currency();
  }

  /** Format number with the current workspace/entity currency code */
  format(amount: number | null | undefined, locale = 'en-US'): string {
    const val = amount ?? 0;
    const formatted = new Intl.NumberFormat(locale, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(val);
    return `${this.currency()} ${formatted}`;
  }
}

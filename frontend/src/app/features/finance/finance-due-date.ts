import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { toIsoDate } from '@core/format/dates';

/** The controls of a drawer whose due date follows the party's payment terms. */
export interface DueDateControls {
  party: FormControl<string | null>;
  /** the document date (invoice date, bill date) */
  date: FormControl<Date | null>;
  due: FormControl<Date | null>;
}

const addDays = (d: Date, days: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + days);

/**
 * Keeps an invoice or bill drawer's due date at document date + the party's payment terms, as the API does
 * (Invoicing.cs / BillFeatures.cs), until the user picks a due date of their own. Clearing the due date hands it back to the terms.
 * Create it in the page's constructor (it needs the injection context to stop listening when the page goes).
 */
export class TermsDueDate {
  /** Payment terms (days) per party id; null = the party has no terms. */
  private readonly terms = new Map<string, number | null>();
  /** True once the user picked a due date themselves; then date / party changes no longer move it. */
  private touched = false;
  private syncing = false;

  constructor(private readonly c: DueDateControls, private readonly parties: PartiesApi, private readonly editable: () => boolean) {
    c.party.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => { if (!this.syncing && id) this.withTerms(id, () => this.recalc()); });
    c.date.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => { if (!this.syncing) this.recalc(); });
    c.due.valueChanges.pipe(takeUntilDestroyed()).subscribe((d) => {
      if (this.syncing) return;
      this.touched = !!d;
      if (!d) this.recalc();
    });
  }

  /** Notes a party's terms (from a search result or a lookup), so choosing it needs no further request. */
  remember(p: PartyDto): void { this.terms.set(p.id, p.paymentTermsDays ?? null); }

  /** Fills the form without the due date reacting to it (showing a document). */
  quietly(fill: () => void): void {
    this.syncing = true;
    fill();
    this.syncing = false;
  }

  /**
   * After a document is shown: a new one follows the terms; an opened draft keeps its due date as an override only if it differs
   * from date + terms. Same rule as the API: no terms counts as 0 days, so such a draft is "automatic" when due = document date.
   */
  follow(doc: { partyId: string } | null): void {
    this.touched = !!doc;
    if (doc && this.editable()) this.withTerms(doc.partyId, () => {
      const days = this.terms.get(doc.partyId) ?? 0, d = this.c.date.value, due = this.c.due.value;
      const auto = d ? toIsoDate(addDays(d, days)) : null;
      this.touched = !due || toIsoDate(due) !== auto;
    });
  }

  /** Due date = document date + the party's terms. No terms: the due date is left alone. */
  private termsDueDate(date: Date | null, partyId: string | null): Date | null {
    const days = partyId ? this.terms.get(partyId) : undefined;
    if (!date || days === undefined || days === null) return null;
    return addDays(date, days);
  }

  private recalc(): void {
    if (this.touched || !this.editable()) return;
    const due = this.termsDueDate(this.c.date.value, this.c.party.value);
    if (!due) return;
    this.quietly(() => this.c.due.setValue(due));
  }

  /** Runs `then` once the party's payment terms are known (from the search results or a single lookup). */
  private withTerms(partyId: string, then: () => void): void {
    if (this.terms.has(partyId)) { then(); return; }
    this.parties.get(partyId).subscribe({
      next: (p) => { this.terms.set(partyId, p.paymentTermsDays ?? null); if (this.c.party.value === partyId) then(); },
      error: () => undefined,
    });
  }
}

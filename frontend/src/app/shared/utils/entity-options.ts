import { SessionStore } from '@core/session/session.store';
import { SelectOption } from '../components/record-drawer/record-drawer.component';

/** The legal entities the signed-in person may work in, as options for a select (code and name). */
export function legalEntityOptions(session: SessionStore): SelectOption[] {
  return (session.me()?.legalEntities ?? []).map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` }));
}

/** The entity chosen in the header, else the only one there is, else nothing (the person must choose). */
export function defaultLegalEntityId(session: SessionStore): string | null {
  const entities = session.me()?.legalEntities ?? [];
  const current = session.currentEntityId();
  if (current && entities.some((e) => e.id === current)) return current;
  return entities.length === 1 ? entities[0].id : null;
}

/** First and last day of a calendar month as yyyy-MM-dd, without time-zone shifts. */
export function monthBounds(year: number, month: number): { from: string; to: string } {
  const pad = (n: number) => String(n).padStart(2, '0');
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return { from: `${year}-${pad(month)}-01`, to: `${year}-${pad(month)}-${pad(last)}` };
}

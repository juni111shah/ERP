import { phraseNow, tNow } from '../i18n/translate-now';

const VERBS = {
  saved: '{what} saved', created: '{what} created', added: '{what} added', updated: '{what} updated', deleted: '{what} deleted', removed: '{what} removed',
  approved: '{what} approved', rejected: '{what} rejected', submitted: '{what} submitted', cancelled: '{what} cancelled', archived: '{what} archived', restored: '{what} restored',
  posted: '{what} posted', voided: '{what} voided', sent: '{what} sent', completed: '{what} completed', released: '{what} released', issued: '{what} issued', recorded: '{what} recorded',
} as const;

export type DoneVerb = keyof typeof VERBS;

/**
 * What a success message says: which record, and what happened to it. "Promotion Summer sale deleted", never "Deleted" or "Success".
 *   doneText('deleted', 'Promotion', row.name)
 * The verb sentence comes from the dictionaries and the entity from the phrase table, so it reads properly in Arabic. The name is the
 * record's own number or name, and is left out only when the record has none.
 */
export function doneText(verb: DoneVerb, entity: string, name?: string | number | null): string {
  const shownName = name === null || name === undefined || name === '' ? '' : ` ${name}`;
  return tNow(`done.${verb}`, VERBS[verb], { what: `${phraseNow(entity)}${shownName}` });
}

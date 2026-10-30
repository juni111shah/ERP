import { DestroyRef } from '@angular/core';
import { of, throwError } from 'rxjs';
import { PagedResult } from './api.models';
import { PagedList } from './paged-list';

describe('PagedList error state', () => {
  const page = { items: ['a'], total: 1, page: 1, pageSize: 25 } as unknown as PagedResult<string>;
  const destroyRef = { onDestroy: () => undefined } as unknown as DestroyRef;

  it('exposes a failed load instead of looking like an empty list, and clears it on a successful reload', () => {
    let fail = true;
    const list = new PagedList<string>(() => (fail ? throwError(() => new Error('boom')) : of(page)), destroyRef);
    expect(list.error()).not.toBeNull();
    expect(list.loading()).toBeFalse();
    expect(list.items()).toEqual([]);

    fail = false;
    list.reload();
    expect(list.error()).toBeNull();
    expect(list.items()).toEqual(['a']);
  });

  it('keeps the previous rows when a later reload fails', () => {
    let fail = false;
    const list = new PagedList<string>(() => (fail ? throwError(() => new Error('boom')) : of(page)), destroyRef);
    fail = true;
    list.reload();
    expect(list.error()).not.toBeNull();
    expect(list.items()).toEqual(['a']);
  });
});

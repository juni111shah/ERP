import { Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { ProjectsConfig, ProjectsExtraApi, UserOption } from '@core/api/projects-extra.api';
import { ProjectsApi, ProjectSummaryDto } from '@core/api/projects.api';
import { SelectOption } from '@shared/components/record-drawer/record-drawer.component';

/** The fields a project picker shows. */
export type ProjectPick = Pick<ProjectSummaryDto, 'id' | 'number' | 'name'>;

/** A project as a picker names it: "PRJ-001 · Website". */
export const projectLabel = (p: ProjectPick): string => `${p.number} · ${p.name}`;

/** Projects as select options. */
export function projectOptions(projects: ProjectPick[]): SelectOption[] {
  return projects.map((p) => ({ value: p.id, label: projectLabel(p) }));
}

/** The picker name of the project with this id, or the fallback when it is not in the list. */
export function projectNameIn(projects: ProjectPick[], id: string, fallback: string): string {
  const p = projects.find((x) => x.id === id);
  return p ? projectLabel(p) : fallback;
}

/** People as select options. */
export function userOptions(users: UserOption[]): SelectOption[] {
  return users.map((u) => ({ value: u.id, label: u.fullName }));
}

/** Projects that can still take work, as a signal (empty when they cannot be loaded). Call in an injection context. */
export function injectOpenProjects(): Signal<{ items: ProjectSummaryDto[] }> {
  return toSignal(inject(ProjectsApi).open().pipe(catchError(() => of({ items: [] as ProjectSummaryDto[] }))), { initialValue: { items: [] } });
}

/** Every project that is not a template, by name, as a signal (empty when it cannot be loaded). Call in an injection context. */
export function injectRealProjects(): Signal<{ items: ProjectSummaryDto[] }> {
  const projects$ = inject(ProjectsApi).list({ page: 1, pageSize: 200, sort: 'name', filters: [{ field: 'isTemplate', op: 'eq', value: false }] });
  return toSignal(projects$.pipe(catchError(() => of({ items: [] as ProjectSummaryDto[] }))), { initialValue: { items: [] } });
}

/** The workspace's people, as a signal (empty when they cannot be loaded). Call in an injection context. */
export function injectProjectUsers(): Signal<UserOption[]> {
  return toSignal(inject(ProjectsExtraApi).users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });
}

/** The Projects settings, as a signal (null until loaded, or when they cannot be). Call in an injection context. */
export function injectProjectsConfig(): Signal<ProjectsConfig | null> {
  return toSignal(inject(ProjectsExtraApi).config().pipe(catchError(() => of(null as ProjectsConfig | null))), { initialValue: null });
}

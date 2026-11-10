import { LegalEntityDto } from '@core/api/admin.api';
import { DepartmentDto } from '@core/api/hr-people.api';
import { EmployeeSummary } from '@core/api/hr.api';
import { SelectOption } from '@shared/components/record-drawer/record-drawer.component';

/** The label of `value` in an option list (kinds, statuses, stages...), else the value itself. */
export function optionLabel(options: readonly { value: string; label: string }[], value: string): string {
  return options.find((o) => o.value === value)?.label ?? value;
}

/** Employers for a select: the legal entity's name. */
export function entityOptions(entities: LegalEntityDto[]): SelectOption[] {
  return entities.map((e) => ({ value: e.id, label: e.name }));
}

/** Everyone who still works here (not terminated). */
export function currentEmployees(employees: EmployeeSummary[]): EmployeeSummary[] {
  return employees.filter((e) => e.status !== 'terminated');
}

/** Employees for a select, "EMP-001 · Sara Al Amri". */
export function employeeOptions(employees: EmployeeSummary[]): SelectOption[] {
  return employees.map((e) => ({ value: e.id, label: `${e.number} · ${e.fullName}` }));
}

/** Departments for a select, "FIN · Finance". */
export function departmentOptions(departments: DepartmentDto[]): SelectOption[] {
  return departments.map((d) => ({ value: d.id, label: `${d.code} · ${d.name}` }));
}

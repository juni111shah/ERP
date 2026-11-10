import { LegalEntityDto, UserSummary } from '@core/api/admin.api';
import { DepartmentDto, PositionDto } from '@core/api/hr-people.api';
import { EMPLOYEE_STATUSES, EmployeeSummary } from '@core/api/hr.api';
import { FieldSpec, SelectOption } from '@shared/components/record-drawer/record-drawer.component';
import { currentEmployees, departmentOptions, employeeOptions, entityOptions } from './hr-options';

const CONTRACT_TYPES = [{ value: 'permanent', label: 'Permanent' }, { value: 'fixedTerm', label: 'Fixed term' }, { value: 'probation', label: 'Probation' }, { value: 'partTime', label: 'Part time' }, { value: 'contractor', label: 'Contractor' }];
const GENDERS = [{ value: 'Female', label: 'Female' }, { value: 'Male', label: 'Male' }];
const MARITAL_STATUSES = [{ value: 'Single', label: 'Single' }, { value: 'Married', label: 'Married' }, { value: 'Divorced', label: 'Divorced' }, { value: 'Widowed', label: 'Widowed' }];

/** The KPI cards of the employees page, from the staff list (at most 200 rows: `capped` says the API had more). */
export interface StaffStats { active: number; onLeave: number; totalGross: number; departmentsCount: number; capped: boolean; }

export function staffStats(items: EmployeeSummary[], total: number): StaffStats {
  const employed = currentEmployees(items);
  return {
    active: items.filter((e) => e.status === 'active').length,
    onLeave: items.filter((e) => e.status === 'onLeave').length,
    totalGross: employed.reduce((sum, e) => sum + (e.grossMonthly || 0), 0),
    departmentsCount: new Set(employed.map((e) => e.department).filter(Boolean)).size,
    capped: total > items.length,
  };
}

/** The pick lists of the employee form. */
export interface EmployeeFormLists {
  entities: LegalEntityDto[];
  departments: DepartmentDto[];
  positions: PositionDto[];
  /** everyone on the staff list: the line manager is picked from it */
  staff: EmployeeSummary[];
  users: UserSummary[];
}

/** The salary and bank fields, shown only with hr.salary.read (the API sends no pay figures without it). */
export function employeePayFields(): FieldSpec[] {
  return [
    { key: 'basicSalary', label: 'Basic salary (monthly)', type: 'number', required: true, min: 0, hint: 'Also the end-of-service base.' },
    { key: 'housingAllowance', label: 'Housing allowance', type: 'number', min: 0, default: 0 },
    { key: 'transportAllowance', label: 'Transport allowance', type: 'number', min: 0, default: 0 },
    { key: 'otherAllowances', label: 'Other allowances', type: 'number', min: 0, default: 0 },
    { key: 'recurringDeductions', label: 'Recurring deductions', type: 'number', min: 0, default: 0, hint: 'A fixed monthly deduction. Staff loans have their own screen.' },
    { key: 'iban', label: 'IBAN', type: 'text', maxLength: 50 },
    { key: 'bankName', label: 'Bank name', type: 'text', maxLength: 150 },
    { key: 'labourCardNumber', label: 'Labour card / MOHRE personal no.', type: 'text', maxLength: 30, hint: 'UAE WPS: the 14-digit number used in the salary file.' },
    { key: 'bankRoutingCode', label: 'Bank routing code', type: 'text', maxLength: 20, hint: 'UAE WPS: routing code of the employee\'s bank.' },
  ];
}

/** Active positions as "CODE · title" options. */
function positionOptions(positions: PositionDto[]): SelectOption[] {
  return positions.filter((p) => p.isActive).map((p) => ({ value: p.id, label: `${p.code} · ${p.title}` }));
}

/**
 * The fields of the employee drawer. `selfId` is the employee being edited (they cannot be their own manager); `pay` are the
 * salary fields, or none without hr.salary.read.
 */
export function employeeFields(lists: EmployeeFormLists, selfId: string | undefined, pay: FieldSpec[]): FieldSpec[] {
  return [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, readonlyOnEdit: true, options: entityOptions(lists.entities) },
    { key: 'number', label: 'Employee no.', type: 'text', maxLength: 30, hint: 'Leave blank to auto-number.' },
    { key: 'fullName', label: 'Full name', type: 'text', required: true, maxLength: 200 },
    { key: 'designation', label: 'Designation', type: 'text', required: true, maxLength: 150 },
    { key: 'departmentId', label: 'Department', type: 'select', options: departmentOptions(lists.departments.filter((d) => d.isActive)) },
    { key: 'department', label: 'Department (free text)', type: 'text', maxLength: 100, hint: 'Used when no department is chosen above.' },
    { key: 'positionId', label: 'Position', type: 'select', options: positionOptions(lists.positions) },
    { key: 'managerEmployeeId', label: 'Line manager', type: 'select', options: employeeOptions(currentEmployees(lists.staff).filter((e) => e.id !== selfId)) },
    { key: 'userId', label: 'Sign-in account', type: 'select', hint: 'Links this person to a user so they can use self-service.', options: lists.users.map((u) => ({ value: u.id, label: `${u.fullName} (${u.email})` })) },
    { key: 'joinDate', label: 'Join date', type: 'date', required: true },
    { key: 'status', label: 'Status', type: 'select', options: EMPLOYEE_STATUSES, default: 'active' },
    { key: 'terminationDate', label: 'Termination date', type: 'date', hidden: (v) => v['status'] !== 'terminated' },
    { key: 'contractType', label: 'Contract type', type: 'select', options: CONTRACT_TYPES },
    { key: 'probationEndDate', label: 'Probation ends', type: 'date' },
    { key: 'contractEndDate', label: 'Contract ends', type: 'date' },
    { key: 'noticePeriodDays', label: 'Notice period (days)', type: 'number', min: 0, max: 365 },
    { key: 'email', label: 'Email', type: 'text', maxLength: 200 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 50 },
    { key: 'nationality', label: 'Nationality', type: 'text', maxLength: 100 },
    { key: 'dateOfBirth', label: 'Date of birth', type: 'date' },
    { key: 'gender', label: 'Gender', type: 'select', options: GENDERS },
    { key: 'maritalStatus', label: 'Marital status', type: 'select', options: MARITAL_STATUSES },
    { key: 'address', label: 'Address', type: 'text', maxLength: 500 },
    { key: 'emergencyContactName', label: 'Emergency contact', type: 'text', maxLength: 150 },
    { key: 'emergencyContactPhone', label: 'Emergency phone', type: 'text', maxLength: 50 },
    ...pay,
  ];
}

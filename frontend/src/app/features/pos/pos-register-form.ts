import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** A pick-list entry of the register form. */
export interface RegisterOption {
  value: string;
  label: string;
}

/** A postable account as a pick-list entry, with its type so the form can offer only asset accounts where money lands. */
export interface RegisterAccountOption extends RegisterOption {
  type: string;
}

/** The accounts a register may leave empty; on the server an account left out keeps its value. */
export const CLEARABLE_REGISTER_ACCOUNTS = ['cardAccountId', 'varianceAccountId', 'expenseAccountId'];

/** The register drawer: where the till sells from, which accounts its money goes to and the wording on its receipts. */
export function registerFields(entities: RegisterOption[], warehouses: RegisterOption[], accounts: RegisterAccountOption[]): FieldSpec[] {
  const assets = accounts.filter((a) => a.type?.toLowerCase() === 'asset');
  return [
    { key: 'code', label: 'Register code', type: 'text', required: true, maxLength: 50, readonlyOnEdit: true },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150, hint: 'What cashiers see, such as Front counter 1.' },
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: entities, default: entities[0]?.value, readonlyOnEdit: true },
    { key: 'warehouseId', label: 'Warehouse', type: 'select', searchable: true, options: warehouses, hint: 'Sales take stock from here. Empty uses the first warehouse of the legal entity.' },
    { key: 'defaultCashAccountId', label: 'Cash account', type: 'select', searchable: true, options: assets, hint: 'Empty uses Cash on hand.' },
    { key: 'cardAccountId', label: 'Card takings account', type: 'select', searchable: true, options: assets, hint: 'Empty uses the bank account.' },
    { key: 'varianceAccountId', label: 'Cash over and short account', type: 'select', searchable: true, options: accounts, hint: 'Takes what is over or short when a shift closes. Empty uses general expenses.' },
    { key: 'expenseAccountId', label: 'Pay-out account', type: 'select', searchable: true, options: accounts, hint: 'The other side of cash paid out of the drawer. Empty uses general expenses.' },
    { key: 'receiptHeader', label: 'Receipt header', type: 'textarea', maxLength: 500 },
    { key: 'receiptFooter', label: 'Receipt footer', type: 'textarea', maxLength: 500 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}

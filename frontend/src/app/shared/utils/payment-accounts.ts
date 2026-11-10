import { AccountDto } from '@core/api/admin.api';
import { SelectOption } from '../components/record-drawer/record-drawer.component';

/** An active, postable asset account money can be paid from or into. */
function isActivePostableAsset(a: AccountDto): boolean {
  return a.type === 'asset' && a.isPostable && a.isActive;
}

/** Bank accounts only: flagged as a bank, or with the bank system role. */
export function bankAccounts(accounts: AccountDto[]): AccountDto[] {
  return accounts.filter((a) => isActivePostableAsset(a) && (a.isBank || a.systemRole === 'bank'));
}

/** Bank and cash accounts: flagged as a bank, or with the bank or cash system role. */
export function bankOrCashAccounts(accounts: AccountDto[]): AccountDto[] {
  return accounts.filter((a) => isActivePostableAsset(a) && (a.isBank || a.systemRole === 'bank' || a.systemRole === 'cash'));
}

/** Accounts as select options, "1010 · Main bank". */
export function accountOptions(accounts: AccountDto[]): SelectOption[] {
  return accounts.map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` }));
}

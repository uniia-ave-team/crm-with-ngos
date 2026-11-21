import { AccountPatch, AccountsService, NewAccount, StoredAccount } from './accounts.service';

/**
 * Для тестів: акаунт, у якого вже є знімок (імʼя, ГО, права тощо). Робить те саме, що й
 * застосунок: вхід додає акаунт (`add`), а дані профілю записуються слідом (`update`).
 * Повертає запис уже зі знімком.
 */
export function addAccountWithSnapshot(accounts: AccountsService, input: NewAccount, snapshot: AccountPatch): StoredAccount {
  const { id } = accounts.add(input);
  accounts.update(id, snapshot);
  return accounts.accounts().find((account) => account.id === id) as StoredAccount;
}

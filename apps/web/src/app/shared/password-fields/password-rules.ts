/**
 * Умови до пароля — ті самі, що перевіряє сервер (`IdentityOptions` у
 * налаштуваннях API): від 8 символів, велика й мала латинська літера, цифра та
 * спецсимвол (будь-що, крім латинських літер і цифр).
 */
export const PASSWORD_MIN_LENGTH = 8;

export interface PasswordRule {
  /** Ключ умови: за ним береться текст `common.passwordRequirements.<key>`. */
  readonly key: 'length' | 'lowercase' | 'uppercase' | 'digit' | 'special';
  /** Символ, який умова вимагає (показується в позначці). */
  readonly icon: string;
  readonly test: (password: string) => boolean;
}

export const PASSWORD_RULES: readonly PasswordRule[] = [
  { key: 'length', icon: '8', test: (password) => password.length >= PASSWORD_MIN_LENGTH },
  { key: 'lowercase', icon: 'a', test: (password) => /[a-z]/.test(password) },
  { key: 'uppercase', icon: 'A', test: (password) => /[A-Z]/.test(password) },
  { key: 'digit', icon: '123', test: (password) => /[0-9]/.test(password) },
  { key: 'special', icon: '#', test: (password) => /[^a-zA-Z0-9]/.test(password) },
];

/** Пароль відповідає всім умовам. */
export function isStrongPassword(password: string): boolean {
  return PASSWORD_RULES.every((rule) => rule.test(password));
}

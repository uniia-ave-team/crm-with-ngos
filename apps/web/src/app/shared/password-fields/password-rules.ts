/**
 * Умови до пароля на сервері. Кожна інсталяція налаштовує їх сама (`IdentityOptions` у
 * налаштуваннях API: довжина від 1 до 100, а кожну з чотирьох вимог можна вимкнути) і
 * віддає публічним запитом `GET /api/v1/system/identity-options` (`PasswordPolicyService`).
 */
export interface PasswordPolicy {
  /** Найменша довжина пароля. */
  readonly minLength: number;
  readonly requireLowercase: boolean;
  readonly requireUppercase: boolean;
  readonly requireDigit: boolean;
  /** Спецсимвол — будь-що, крім латинської літери й цифри (кирилична літера теж). */
  readonly requireSpecial: boolean;
}

/** Умови сервера за замовчуванням: ними користуємось, доки сервер не віддав своїх (чи не зміг). */
export const DEFAULT_PASSWORD_POLICY: PasswordPolicy = {
  minLength: 8,
  requireLowercase: true,
  requireUppercase: true,
  requireDigit: true,
  requireSpecial: true,
};

export interface PasswordRule {
  /** Ключ умови: за ним береться текст `common.passwordRequirements.<key>`. */
  readonly key: 'length' | 'lowercase' | 'uppercase' | 'digit' | 'special';
  /** Символ, який умова вимагає (показується в позначці). */
  readonly icon: string;
  /** Параметри тексту умови (`{{count}}` у тексті довжини). */
  readonly params: Readonly<Record<string, number>>;
  readonly test: (password: string) => boolean;
}

/**
 * Умови, які треба показати й перевірити, — ті, що сервер вимагає, у сталому порядку. Літери
 * сервер рахує лише латинські, тож і тут так само.
 */
export function passwordRules(policy: PasswordPolicy = DEFAULT_PASSWORD_POLICY): readonly PasswordRule[] {
  const rules: (PasswordRule | false)[] = [
    {
      key: 'length',
      icon: String(policy.minLength),
      params: { count: policy.minLength },
      test: (password) => password.length >= policy.minLength,
    },
    policy.requireLowercase && { key: 'lowercase', icon: 'a', params: {}, test: (password) => /[a-z]/.test(password) },
    policy.requireUppercase && { key: 'uppercase', icon: 'A', params: {}, test: (password) => /[A-Z]/.test(password) },
    policy.requireDigit && { key: 'digit', icon: '123', params: {}, test: (password) => /[0-9]/.test(password) },
    policy.requireSpecial && { key: 'special', icon: '#', params: {}, test: (password) => /[^a-zA-Z0-9]/.test(password) },
  ];
  return rules.filter((rule): rule is PasswordRule => rule !== false);
}

/** Пароль відповідає всім умовам сервера. */
export function isStrongPassword(password: string, policy: PasswordPolicy = DEFAULT_PASSWORD_POLICY): boolean {
  return passwordRules(policy).every((rule) => rule.test(password));
}

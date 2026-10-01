/**
 * Единственное разрешённое представление тела ответа в коде фронта
 * (ADR-0009): провод `snake_case`, рантайм и UI — `camelCase`, перекладывает
 * интерсептор `shared/api/instances/base.ts`. Query/path-параметры и
 * `application/problem+json` не преобразуются (ADR-0008).
 * Round-trip имён проверяет `scripts/contracts/frontend-case-roundtrip.py`.
 */

/** `some_key` → `someKey`. Ключи без `_` остаются как есть. */
type SnakeToCamelKey<S extends string> = S extends `${infer Head}_${infer Tail}`
  ? `${Head}${Capitalize<SnakeToCamelKey<Tail>>}`
  : S;

/** Рекурсивно, включая массивы; необязательность ключа сохраняется. */
export type Camelized<T> = T extends readonly (infer Item)[]
  ? Camelized<Item>[]
  : T extends object
    ? { [K in keyof T as SnakeToCamelKey<Extract<K, string>>]: Camelized<T[K]> }
    : T;

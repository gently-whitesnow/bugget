/**
 * Мост между проводом и кодом фронта — только на уровне типов.
 *
 * Контракт (`shared/api/generated/*.d.ts`) описывает провод, то есть `snake_case`.
 * Ответы, которые не попали в `skipCaseConversionPatterns`
 * (`shared/api/instances/base.ts`), интерсептор перекладывает в `camelCase` ещё до
 * того, как их увидит код. Объявить такой ответ сгенерированным типом напрямую —
 * значит соврать компилятору.
 *
 * `Camelized<T>` выводит camelCase-форму из сгенерированной схемы, поэтому источником
 * правды остаётся yaml: удалили ключ в контракте — он пропал и здесь, а обращение к
 * нему стало ошибкой компиляции. Второго, независимого DTO при этом не появляется.
 *
 * Это точечный мост для уже мигрированных ручек, а не общее решение границы
 * представлений: единый case-boundary — отдельная задача.
 */

/** `some_key` → `someKey`. Ключи без `_` остаются как есть. */
type SnakeToCamelKey<S extends string> = S extends `${infer Head}_${infer Tail}`
  ? `${Head}${Capitalize<SnakeToCamelKey<Tail>>}`
  : S;

/**
 * Рекурсивно переводит ключи объекта (включая элементы массивов и вложенные
 * объекты) из `snake_case` в `camelCase`. Примитивы, `null` и `undefined`
 * проходят насквозь; необязательность ключа сохраняется.
 */
export type Camelized<T> = T extends readonly (infer Item)[]
  ? Camelized<Item>[]
  : T extends object
    ? { [K in keyof T as SnakeToCamelKey<Extract<K, string>>]: Camelized<T[K]> }
    : T;

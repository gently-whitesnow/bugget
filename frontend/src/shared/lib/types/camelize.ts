/**
 * Мост между проводом и кодом фронта — только на уровне типов.
 *
 * Правило границы одно на весь фронт:
 *
 *   * `shared/api/generated/*.d.ts` — провод. Тело HTTP-запроса и ответа там
 *     `snake_case`, потому что таким его описывает `specs/contracts/**`.
 *   * HTTP-рантайм и UI — `camelCase`. Тела ответов перекладывает интерсептор
 *     (`shared/api/instances/base.ts`), тела запросов он же кладёт обратно в
 *     `snake_case`. URL-исключений из конверсии нет.
 *   * Query- и path-параметры не преобразуются: их имена (`teamId`, `period`,
 *     `aliasId`) — часть публичного контракта, и берутся они из generated
 *     напрямую, без `Camelized`.
 *   * `application/problem+json` не преобразуется вовсе: в `errors` лежит
 *     словарь «wire-имя поля → ошибки», и конверсия переписала бы имена полей
 *     формы (ADR-0008, MAIN-74/67/69).
 *
 * Отсюда `Camelized<T>` — единственное разрешённое представление тела ответа в
 * коде фронта. Объявить ответ сгенерированным типом напрямую — соврать
 * компилятору; описать его рукописным DTO — завести второе независимое
 * представление данных, которое разойдётся с контрактом молча. Источником
 * правды остаётся yaml: удалили ключ в контракте — он пропал и здесь, а
 * обращение к нему стало ошибкой компиляции.
 *
 * Гейт `frontend-contracts` дополнительно требует, чтобы имена полей тела
 * переживали round-trip `wire → camelCase → wire`
 * (`scripts/quality/frontend-case-roundtrip.py`): имя, которое его не проходит,
 * этой проекцией не выражается, и контракт с таким именем не принимается.
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

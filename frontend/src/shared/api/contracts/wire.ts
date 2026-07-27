/**
 * Мост между wire-форматом контракта и тем, что видит код фронта.
 *
 * Контракты (`specs/contracts/**\/openapi.yaml`) описывают JSON как он ходит по
 * проводу — snake_case. Код фронта работает с camelCase, потому что
 * `shared/api/instances/base.ts` перекладывает ключи интерсепторами
 * (`convertObjectToCamel` на ответе, `convertObjectToSnake` на запросе).
 *
 * `Wire<T>` повторяет ту же перекладку на уровне типов. Благодаря этому DTO
 * фронта выводятся из сгенерированного контракта, а не переписываются руками:
 * поле, переименованное или удалённое в yaml, ломает компиляцию в местах
 * обращения, а не всплывает 404-й или `undefined` у заказчика.
 *
 * Почему не отключить конверсию и не взять snake_case напрямую, как сделано для
 * `/v2/analytics/*`: там DTO читает пара виджетов, а `Report`/`Bug`/`Comment`
 * читает весь UI. Отключение интерсептора для них — это переименование полей в
 * сотнях мест, то есть ровно то изменение поведения, которого задача просит
 * избежать. Тип-мост даёт ту же защиту от дрейфа ценой нулевого рантайма.
 *
 * Ограничение: `SnakeToCamel` совпадает с рантаймовым `snakeToCamel` на ключах
 * вида `foo_bar_baz`. Ключ с ведущим подчёркиванием (`_foo`) рантайм и тип
 * переложат по-разному; в контрактах таких ключей нет, и гейт `frontend-contracts`
 * вместе с тестом `wire.test.ts` держит это утверждение проверяемым.
 */

/** `snake_case` → `camelCase` на уровне типов. Зеркало `shared/lib/convertCases.ts`. */
export type SnakeToCamel<S extends string> =
  S extends `${infer Head}_${infer Tail}`
    ? `${Head}${Capitalize<SnakeToCamel<Tail>>}`
    : S;

/**
 * Рекурсивно переводит ключи wire-DTO в camelCase, сохраняя опциональность
 * полей, массивы, `null` в объединениях и примитивы как есть.
 */
export type Wire<T> = T extends readonly (infer Item)[]
  ? Wire<Item>[]
  : T extends object
    ? { [K in keyof T as SnakeToCamel<K & string>]: Wire<T[K]> }
    : T;

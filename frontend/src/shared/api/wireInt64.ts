/**
 * Канон `Int64String` с провода (`specs/contracts/shared.yaml`).
 *
 * Неотрицательный 64-битный идентификатор и счётчик приходят строкой, потому что
 * `number` здесь — IEEE-754 double: `Number("9007199254740993")` даёт
 * `9007199254740992`, и дальше этим значением уже нельзя пользоваться — ни ссылкой,
 * ни ключом списка, ни параметром запроса. Поэтому строка хранится строкой, а
 * сравнивается — точно, через `BigInt`. `Number(...)` к таким значениям не
 * применяется нигде.
 *
 * Канон: `0` либо `[1-9][0-9]*` без знака, ведущих нулей, экспоненты и
 * разделителей, в диапазоне `0..9223372036854775807`.
 */

const CANONICAL = /^(0|[1-9][0-9]*)$/;

const MAX = 9223372036854775807n;

/** Значение с провода в каноне `Int64String`. */
export type WireInt64 = string;

/**
 * Проверка канона — для того, что пришло не с провода: сегмента адреса, значения
 * из query, введённого пользователем текста. Тела ответов проверять не нужно:
 * их форму держит контракт.
 */
export const isWireInt64 = (value: unknown): value is WireInt64 =>
  typeof value === "string" && CANONICAL.test(value) && BigInt(value) <= MAX;

/**
 * Канон → `BigInt`. Неканоничное значение — это сломанный контракт, а не данные:
 * молча превращать его в `0` значит спрятать поломку в UI.
 */
export const wireInt64ToBigInt = (value: WireInt64): bigint => {
  if (!isWireInt64(value)) {
    throw new TypeError(`Не канон Int64String: ${JSON.stringify(value)}`);
  }
  return BigInt(value);
};

/**
 * Точное сравнение: `< 0`, `0`, `> 0` — как у компаратора.
 *
 * Правая сторона может быть числом: так сравнивают с величиной, которую считает
 * сам клиент (размер страницы, длина уже загруженного списка). Значения с провода
 * числом не бывают — для них обе стороны строки.
 */
export const compareWireInt64 = (
  left: WireInt64,
  right: WireInt64 | number
): number => {
  const a = wireInt64ToBigInt(left);
  const b =
    typeof right === "number" ? BigInt(right) : wireInt64ToBigInt(right);

  if (a < b) return -1;
  return a > b ? 1 : 0;
};

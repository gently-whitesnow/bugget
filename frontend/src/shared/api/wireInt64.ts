// Канон `Int64String` (`specs/contracts/shared.yaml`): `0` либо `[1-9][0-9]*`,
// до 9223372036854775807. `number` теряет точность за 2^53−1, поэтому значение
// хранится строкой и сравнивается через `BigInt`; `Number(...)` не применяется.

const CANONICAL = /^(0|[1-9][0-9]*)$/;

const MAX = 9223372036854775807n;

export type WireInt64 = string;

/** Для значений не с провода (адрес, query, ввод): тела держит контракт. */
export const isWireInt64 = (value: unknown): value is WireInt64 =>
  typeof value === "string" && CANONICAL.test(value) && BigInt(value) <= MAX;

// Неканоничное значение — сломанный контракт: молчаливый `0` спрятал бы поломку.
export const wireInt64ToBigInt = (value: WireInt64): bigint => {
  if (!isWireInt64(value)) {
    throw new TypeError(`Не канон Int64String: ${JSON.stringify(value)}`);
  }
  return BigInt(value);
};

// Компаратор. Число справа — только величина, посчитанная самим клиентом
// (размер страницы); значения с провода числом не бывают.
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

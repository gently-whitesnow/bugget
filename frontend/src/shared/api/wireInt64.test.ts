import { describe, expect, it } from "vitest";

import { compareWireInt64, isWireInt64, wireInt64ToBigInt } from "./wireInt64";

/**
 * Канон `Int64String` с провода. Значения выбраны по границам, а не по вкусу:
 * `9007199254740993` — первое целое, которого нет в double, а
 * `9223372036854775807` — верхняя граница Int64.
 */
const UNSAFE = "9007199254740993";
const MAX = "9223372036854775807";

describe("канон Int64String на клиенте", () => {
  it("значение за пределом точности double остаётся строкой цифра в цифру", () => {
    expect(isWireInt64(UNSAFE)).toBe(true);
    expect(wireInt64ToBigInt(UNSAFE)).toBe(9007199254740993n);

    // Ровно то, ради чего поле стало строкой: число этот идентификатор теряет.
    expect(String(Number(UNSAFE))).toBe("9007199254740992");
    expect(UNSAFE).not.toBe(String(Number(UNSAFE)));
  });

  it.each([["0"], ["1"], [UNSAFE], ["922337203685477580"], [MAX]])(
    "принимает канон: %s",
    (value) => {
      expect(isWireInt64(value)).toBe(true);
    }
  );

  it.each([
    [""],
    [" "],
    ["-1"],
    ["+1"],
    ["007"],
    ["1.0"],
    ["1e3"],
    ["1_000"],
    [" 1"],
    ["1 "],
    ["abc"],
    ["0x10"],
    ["١٢٣"],
    ["9223372036854775808"],
    ["99999999999999999999"],
  ])("отвергает неканоничное: %j", (value) => {
    expect(isWireInt64(value)).toBe(false);
  });

  it("нестрока каноном не является", () => {
    expect(isWireInt64(42)).toBe(false);
    expect(isWireInt64(null)).toBe(false);
    expect(isWireInt64(undefined)).toBe(false);
  });

  it("сравнение точное: соседние значения за 2^53 не сливаются", () => {
    expect(compareWireInt64("9007199254740992", UNSAFE)).toBeLessThan(0);
    expect(compareWireInt64(UNSAFE, "9007199254740992")).toBeGreaterThan(0);
    expect(compareWireInt64(UNSAFE, UNSAFE)).toBe(0);

    // Через Number обе стороны схлопнулись бы в одно значение.
    expect(Number("9007199254740992")).toBe(Number(UNSAFE));
  });

  it("сравнение с величиной клиента: длина списка и размер страницы — числа", () => {
    expect(compareWireInt64(MAX, 10)).toBeGreaterThan(0);
    expect(compareWireInt64("3", 10)).toBeLessThan(0);
    expect(compareWireInt64("10", 10)).toBe(0);
  });

  it("неканоничное значение — сломанный контракт, а не молчаливый ноль", () => {
    expect(() => wireInt64ToBigInt("007")).toThrow(TypeError);
    expect(() => compareWireInt64("9223372036854775808", 1)).toThrow(TypeError);
  });
});

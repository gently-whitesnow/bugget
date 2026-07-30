import { describe, expect, it, vi } from "vitest";

const post = vi.fn();

vi.mock("@/shared/api", () => ({
  appApi: {
    post: (...args: unknown[]) => post(...args),
  },
}));

const { fetchReportCountsFx } = await import("./reportCounts");

/**
 * Счётчики приходят массивом `[{ key, count }]`, а не картой со свободными
 * ключами: ключ среза — данные клиента, и в объекте интерсептор переписал бы его
 * вместе с именами полей. Здесь проверяется, что ключ доезжает до стора
 * дословно — с `_` и заглавными.
 */
describe("батч-счётчики репортов", () => {
  it("массив counts раскладывается в карту, ключ среза не преобразуется", async () => {
    post.mockResolvedValueOnce({
      data: {
        counts: [
          { key: "my_scope_key", count: 3 },
          { key: "MyScopeKey", count: 4 },
          { key: "Mixed_Case_KEY", count: 0 },
        ],
      },
    });

    const counts = await fetchReportCountsFx([
      { key: "my_scope_key" },
      { key: "MyScopeKey" },
      { key: "Mixed_Case_KEY" },
    ]);

    expect(counts).toEqual({
      my_scope_key: 3,
      MyScopeKey: 4,
      Mixed_Case_KEY: 0,
    });
  });

  it("срез уходит в теле запроса camelCase — в snake_case его переводит интерсептор", async () => {
    post.mockResolvedValueOnce({ data: { counts: [] } });

    await fetchReportCountsFx([
      { key: "beta-active", teamId: "t1", creatorTypes: [1], statuses: [0, 2] },
    ]);

    expect(post).toHaveBeenLastCalledWith("/v2/reports/counts:batch", {
      scopes: [
        {
          key: "beta-active",
          teamId: "t1",
          creatorTypes: [1],
          statuses: [0, 2],
        },
      ],
    });
  });
});

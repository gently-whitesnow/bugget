// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { appApi, setAppContext } from "@/shared/api";
import { fetchReportCountsFx } from "./reportCounts";

/**
 * Счётчики приходят массивом `[{ key, count }]`, а не картой со свободными
 * ключами: ключ среза — данные клиента, и в объекте интерсептор переписал бы его
 * вместе с именами полей. Здесь проверяется, что ключ доезжает до стора
 * дословно — с `_` и заглавными, — а имена полей среза наоборот уходят на провод
 * в snake_case.
 *
 * Тест идёт через настоящий транспорт (подменён только адаптер axios), поэтому
 * проверяет и операцию из `shared/api/reports`, и интерсепторы, а не мок.
 */

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;
let responseBody: unknown = { counts: [] };

beforeEach(() => {
  setAppContext(1, 2);
  captured = null;
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: responseBody,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
  responseBody = { counts: [] };
});

describe("батч-счётчики репортов", () => {
  it("массив counts раскладывается в карту, ключ среза не преобразуется", async () => {
    responseBody = {
      counts: [
        { key: "my_scope_key", count: 3 },
        { key: "MyScopeKey", count: 4 },
        { key: "Mixed_Case_KEY", count: 0 },
      ],
    };

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

  it("срез уходит на провод в snake_case, значение ключа — дословно", async () => {
    await fetchReportCountsFx([
      { key: "beta-active", teamId: "t1", creatorTypes: [1], statuses: [0, 2] },
    ]);

    expect(captured?.url).toBe(
      "/api/app/workspaces/1/teams/2/v2/reports/counts:batch"
    );
    expect(JSON.parse(captured?.data as string)).toEqual({
      scopes: [
        {
          key: "beta-active",
          team_id: "t1",
          creator_types: [1],
          statuses: [0, 2],
        },
      ],
    });
  });
});

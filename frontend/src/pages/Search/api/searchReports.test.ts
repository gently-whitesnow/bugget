// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { appApi, setAppContext } from "@/shared/api";
import { searchReports } from "./searchReports";

/**
 * Поиск остался на `/v1/reports/search` с историческими camelCase-именами
 * параметров. Имена теперь берутся из контракта, но URL обязан остаться прежним.
 */

const contextPrefix = "/api/app/workspaces/1/teams/2";

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;

beforeEach(() => {
  setAppContext(1, 2);
  captured = null;
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: { total: 0, reports: [] },
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
});

describe("searchReports", () => {
  it("собирает URL из имён контракта, статусы — повторяющимся ключом", async () => {
    await searchReports({
      query: "падает",
      sort: "created",
      userId: "u-1",
      teamId: "t-1",
      skip: 10,
      take: 10,
      reportStatuses: ["backlog", "test"],
    });

    expect(captured?.url).toBe(
      `${contextPrefix}/v1/reports/search?query=%D0%BF%D0%B0%D0%B4%D0%B0%D0%B5%D1%82&sort=created&userId=u-1&teamId=t-1&skip=10&take=10&reportStatuses=backlog&reportStatuses=test`
    );
  });

  it("пустые фильтры в URL не попадают", async () => {
    await searchReports({ query: undefined, skip: 0, take: 10 });

    expect(captured?.url).toBe(
      `${contextPrefix}/v1/reports/search?skip=0&take=10`
    );
  });

  /**
   * Characterization: до миграции поиск всегда клеил `?${searchParams}`, поэтому
   * при пустых фильтрах адрес заканчивался одиноким `?`. Провод в этом слайсе не
   * меняется — значит и этот адрес остаётся прежним.
   */
  it("совсем пустой набор фильтров сохраняет хвостовой «?»", async () => {
    await searchReports({});

    expect(captured?.url).toBe(`${contextPrefix}/v1/reports/search?`);
  });
});

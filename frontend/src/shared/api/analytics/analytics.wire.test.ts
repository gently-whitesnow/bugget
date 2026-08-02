import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { appApi, setAppContext } from "@/shared/api";
import type { components } from "@/shared/api/generated/analytics";
import {
  getAnalyticsByResponsible,
  getAnalyticsSummary,
  getReportAnalytics,
} from "./index";

/**
 * Провод модуля `analytics` после перевода на сгенерированный контракт.
 *
 * Адрес, имена query и их присутствие в URL обязаны остаться прежними: фронт
 * стоит в проде у заказчика. Имена query — camelCase (`teamId`), они часть
 * публичного контракта и конверсию не проходят (ADR-0009); тело ответа, наоборот,
 * приходит snake_case и перекладывается интерсептором.
 */

const contextPrefix = "/api/app/workspaces/1/teams/2";

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;

const sent = () => {
  if (!captured) throw new Error("Запрос не был отправлен");
  return captured;
};

const wireResponsible: components["schemas"]["AnalyticsResponsible"] = {
  period: { from: "2026-07-01", to: "2026-07-30", label: "Июль" },
  reports_participated: [
    { report_id: "1", title: "Первый", current_phase: "Test" },
  ],
  reports_completed: [
    {
      report_id: "2",
      title: "Второй",
      closed_at: "2026-07-20T10:00:00Z",
      outcome: "Resolved",
    },
  ],
  avg_fix_phase_days: null,
};

const respondWith = (data: unknown) => {
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
};

beforeEach(() => {
  setAppContext(1, 2);
  captured = null;
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  respondWith(null);
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
});

describe("адреса аналитики", () => {
  it("сводка без фильтра команды уходит с одним period", async () => {
    await getAnalyticsSummary("30d");

    expect(sent().method).toBe("get");
    expect(sent().url).toBe(`${contextPrefix}/v2/analytics/summary?period=30d`);
  });

  it("сводка с командой добавляет teamId тем же именем", async () => {
    await getAnalyticsSummary("7d", "team-9");

    expect(sent().url).toBe(
      `${contextPrefix}/v2/analytics/summary?period=7d&teamId=team-9`
    );
  });

  it("пустой фильтр команды не превращается в teamId= на проводе", async () => {
    await getAnalyticsSummary("all", "");
    expect(sent().url).toBe(`${contextPrefix}/v2/analytics/summary?period=all`);

    await getAnalyticsSummary("all", null);
    expect(sent().url).toBe(`${contextPrefix}/v2/analytics/summary?period=all`);
  });

  it("сводка по ответственному: id в пути, period в query", async () => {
    respondWith(wireResponsible);

    const responsible = await getAnalyticsByResponsible("user-1", "180d");

    expect(sent().url).toBe(
      `${contextPrefix}/v2/analytics/responsible/user-1?period=180d`
    );
    // Ответ доезжает в camelCase, nullable-значение сохраняется.
    expect(responsible.reportsParticipated[0].reportId).toBe("1");
    expect(responsible.reportsParticipated[0].currentPhase).toBe("Test");
    expect(responsible.reportsCompleted[0].closedAt).toBe(
      "2026-07-20T10:00:00Z"
    );
    expect(responsible.reportsCompleted[0].outcome).toBe("Resolved");
    expect(responsible.avgFixPhaseDays).toBeNull();
    expect(responsible).not.toHaveProperty("avg_fix_phase_days");
  });

  it("detail по репорту остаётся sub-resource модуля reports", async () => {
    await getReportAnalytics("12");

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/12/analytics`);
  });

  it("идентификатор за пределом точности double уходит в адрес цифра в цифру", async () => {
    // `Number("9007199254740993")` даёт ...992 — адрес уехал бы на соседний репорт.
    const reportId = "9007199254740993";

    await getReportAnalytics(reportId);

    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/${reportId}/analytics`
    );
    expect(sent().url).not.toContain("9007199254740992");
  });
});

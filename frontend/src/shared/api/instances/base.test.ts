// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { createApiInstance } from "./base";

/**
 * Подменяем транспорт: интерцепторы отрабатывают ровно так же, как на живом ответе,
 * но без сети.
 */
const instanceRespondingWith = (data: unknown, contentType: string) => {
  const instance = createApiInstance();
  const adapter: AxiosAdapter = async (config) => ({
    data,
    status: 200,
    statusText: "OK",
    headers: { "content-type": contentType },
    config,
  });
  instance.defaults.adapter = adapter;
  return instance;
};

/** Транспорт, который вместо ответа возвращает то, что интерцептор собрал в запросе. */
const instanceCapturingRequest = () => {
  let captured: InternalAxiosRequestConfig | null = null;
  const instance = createApiInstance();
  instance.defaults.adapter = async (config) => {
    captured = config;
    return {
      data: null,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  };
  return { instance, sent: () => captured! };
};

describe("response-интерцептор: case-конверсия", () => {
  it("не конвертирует ключи application/problem+json — словарь errors остаётся как есть", async () => {
    const instance = instanceRespondingWith(
      {
        type: "urn:bugget:error:model_state_validation_error",
        status: 400,
        code: "model_state_validation_error",
        errors: { report_title: ["Поле обязательно"], expected_result: ["!"] },
      },
      "application/problem+json; charset=utf-8"
    );

    const response = await instance.get("/v2/reports");

    expect(response.data.errors).toEqual({
      report_title: ["Поле обязательно"],
      expected_result: ["!"],
    });
    expect(response.data.code).toBe("model_state_validation_error");
  });

  it("обычный application/json конвертирует в camelCase, как и раньше", async () => {
    const instance = instanceRespondingWith(
      { report_title: "Падает карточка" },
      "application/json"
    );

    const response = await instance.get("/v2/reports");

    expect(response.data).toEqual({ reportTitle: "Падает карточка" });
  });

  it("не падает на ответе без Content-Type", async () => {
    const instance = createApiInstance();
    instance.defaults.adapter = async (config) => ({
      data: { report_title: "Падает карточка" },
      status: 200,
      statusText: "OK",
      headers: {},
      config,
    });

    const response = await instance.get("/v2/reports");

    expect(response.data).toEqual({ reportTitle: "Падает карточка" });
  });

  it("конвертирует вложенные объекты, массивы и не трогает null", async () => {
    const instance = instanceRespondingWith(
      {
        report_id: 7,
        phase_timeline: [
          { entered_at: "2026-07-01", exited_at: null, duration_days: 1.5 },
        ],
        bugs_by_status: { open: 1, verified: 0 },
        avg_full_cycle_days: null,
      },
      "application/json"
    );

    const response = await instance.get("/v2/reports/7/analytics");

    expect(response.data).toEqual({
      reportId: 7,
      phaseTimeline: [
        { enteredAt: "2026-07-01", exitedAt: null, durationDays: 1.5 },
      ],
      bugsByStatus: { open: 1, verified: 0 },
      avgFullCycleDays: null,
    });
  });

  it("analytics больше не исключение: /v2/analytics/* конвертируется как все", async () => {
    const instance = instanceRespondingWith(
      {
        period: { from: "2026-07-01", to: "2026-07-30", label: "Июль" },
        avg_phase_duration_days: { test_initial: 2, test_retest: null },
        top_regression_reports: [{ report_id: 3, regression_cycles: 2 }],
      },
      "application/json"
    );

    const response = await instance.get("/v2/analytics/summary?period=month");

    expect(response.data).toEqual({
      period: { from: "2026-07-01", to: "2026-07-30", label: "Июль" },
      avgPhaseDurationDays: { testInitial: 2, testRetest: null },
      topRegressionReports: [{ reportId: 3, regressionCycles: 2 }],
    });
  });

  it("не-JSON тело не трогается: бинарное вложение доезжает целым", async () => {
    // Рекурсивный обход по ключам превратил бы Blob в пустой объект: у него нет
    // собственных перечислимых свойств.
    const attachment = new Blob(["screenshot"], { type: "image/png" });
    const instance = instanceRespondingWith(attachment, "image/png");

    const response = await instance.get("/v2/attachments/1");

    expect(response.data).toBe(attachment);
  });

  it("значения не трогаются: ключи среза в counts доезжают дословно", async () => {
    const instance = instanceRespondingWith(
      {
        counts: [
          { key: "my_scope_key", count: 3 },
          { key: "MyScopeKey", count: 4 },
        ],
      },
      "application/json"
    );

    const response = await instance.post("/v2/reports/counts:batch");

    expect(response.data.counts).toEqual([
      { key: "my_scope_key", count: 3 },
      { key: "MyScopeKey", count: 4 },
    ]);
  });
});

describe("request-интерцептор: case-конверсия", () => {
  it("тело запроса уходит на провод в snake_case, включая вложенное и массивы", async () => {
    const { instance, sent } = instanceCapturingRequest();

    await instance.post("/v2/reports/counts:batch", {
      scopes: [{ key: "beta", teamId: "t1", creatorTypes: [1] }],
      isExcludedFromAnalytics: true,
    });

    // К adapter'у тело доезжает уже сериализованным — сверяем то, что уйдёт на провод.
    expect(JSON.parse(sent().data as string)).toEqual({
      scopes: [{ key: "beta", team_id: "t1", creator_types: [1] }],
      is_excluded_from_analytics: true,
    });
  });

  it("multipart не конвертируется — FormData уходит как есть", async () => {
    const { instance, sent } = instanceCapturingRequest();
    const form = new FormData();
    form.append("file_name", "screen.png");

    await instance.post("/v2/reports/1/attachments", form, {
      headers: { "Content-Type": "multipart/form-data" },
    });

    expect(sent().data).toBe(form);
  });

  it("query-параметры не конвертируются: их имена — часть публичного контракта", async () => {
    const { instance, sent } = instanceCapturingRequest();

    await instance.get("/v2/analytics/summary", {
      params: { period: "month", teamId: "t1" },
    });

    expect(sent().params).toEqual({ period: "month", teamId: "t1" });
  });
});

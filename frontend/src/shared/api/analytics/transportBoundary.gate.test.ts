// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectAnalyticsTransportOptions } from "../../../../eslint-rules/no-direct-analytics-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `analytics` вне транспортной границы.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": ["error", ...noDirectAnalyticsTransportOptions],
    },
  });

describe("гейт прямых вызовов путей analytics", () => {
  it("краснеет на строковом пути и на пути с подстановкой", () => {
    const messages = lint('appApi.get("/v2/analytics/summary");');

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/analytics");
    expect(
      lint("appApi.get(`/v2/analytics/responsible/${userId}`);")
    ).toHaveLength(1);
  });

  it("краснеет на config-form и на обходе axios через fetch", () => {
    expect(
      lint('appApi.request({ url: "/v2/analytics/summary", method: "get" });')
    ).toHaveLength(1);
    expect(lint('appApi({ url: "/v2/analytics/summary" });')).toHaveLength(1);
    expect(lint('fetch("/v2/analytics/summary");')).toHaveLength(1);
  });

  it("вызов операции контракта не трогает", () => {
    expect(lint('request("/v2/analytics/summary", "get", { query });')).toEqual(
      []
    );
  });

  it("detail репорта остаётся за модулем reports", () => {
    // `/v2/reports/{id}/analytics` — sub-resource репорта, его закрывает правило
    // reports: одно сообщение на путь, и оно указывает на нужную границу.
    expect(lint("appApi.get(`/v2/reports/${id}/analytics`);")).toEqual([]);
  });
});

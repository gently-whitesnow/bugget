// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectReportsTransportOptions } from "../../../../eslint-rules/no-direct-reports-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `reports` вне транспортной границы.
 *
 * Тест проверяет саму красноту правила — иначе правило, которое ничего не ловит,
 * выглядит как выполненный инвариант.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": ["error", ...noDirectReportsTransportOptions],
    },
  });

describe("гейт прямых вызовов путей reports", () => {
  it("краснеет на строковом пути в axios-вызове", () => {
    const messages = lint('appApi.get("/v2/reports");');

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/reports");
  });

  it("краснеет на template-пути с подстановкой id", () => {
    expect(lint("appApi.patch(`/v2/reports/${id}`, body);")).toHaveLength(1);
  });

  it("краснеет и на legacy-пути поиска в /v1", () => {
    expect(lint('appApi.get("/v1/reports/search?take=10");')).toHaveLength(1);
  });

  it("краснеет на вложенных путях модуля", () => {
    expect(
      lint("appApi.post(`/v2/reports/${id}/bugs/${bugId}/comments`, body);")
    ).toHaveLength(1);
  });

  it("краснеет на config-form: путь в поле url у .request", () => {
    const messages = lint(
      'appApi.request({ url: "/v2/reports", method: "get" });'
    );

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/reports");
  });

  it("краснеет на config-form с template-путём и на разложенном конфиге", () => {
    expect(
      lint("appApi.request({ method: `get`, url: `/v2/reports/${id}/bugs` });")
    ).toHaveLength(1);
    expect(
      lint('appApi.request({ ...config, url: "/v2/reports/counts:batch" });')
    ).toHaveLength(1);
  });

  it("краснеет на вызове самого инстанса и на пути внутри config второго аргумента", () => {
    expect(lint('appApi({ url: "/v2/reports" });')).toHaveLength(1);
    expect(
      lint("appApi.get(base, { url: `/v2/reports/${id}`, timeout: 1000 });")
    ).toHaveLength(1);
  });

  it("краснеет на обходе axios через fetch", () => {
    expect(
      lint('fetch("/v2/reports/counts:batch", { method: "POST" });')
    ).toHaveLength(1);
  });

  it("вызов операции контракта не трогает", () => {
    const messages = lint(
      'request("/v2/reports/{aliasId}", "get", { path: { aliasId } });'
    );

    expect(messages).toEqual([]);
  });

  it("пути других модулей не трогает", () => {
    expect(lint('appApi.get("/v2/analytics/summary");')).toEqual([]);
    expect(lint("appApi.put(`/v1/user-settings/${id}`, body);")).toEqual([]);
  });
});

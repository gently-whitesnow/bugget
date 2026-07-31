// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectExternalTransportOptions } from "../../../../eslint-rules/no-direct-external-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `external` вне транспортной границы.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": ["error", ...noDirectExternalTransportOptions],
    },
  });

describe("гейт прямых вызовов путей external", () => {
  it("краснеет на поиске и на привязке результата", () => {
    const messages = lint('appApi.get("/v1/external/search?query=a");');

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/external");
    expect(
      lint('appApi.post("/v1/external/search/apply", payload);')
    ).toHaveLength(1);
  });

  it("краснеет на досках Kaiten в обеих формах вызова", () => {
    expect(lint('appApi.get("/v1/external/kaiten/boards");')).toHaveLength(1);
    expect(
      lint('appApi.post("/v1/external/kaiten/boards/batch-get", { ids });')
    ).toHaveLength(1);
    expect(
      lint('appApi.request({ url: "/v1/external/kaiten/boards" });')
    ).toHaveLength(1);
    expect(lint('fetch("/v1/external/search");')).toHaveLength(1);
  });

  it("вызов операции контракта и пути других модулей не трогает", () => {
    expect(lint('request("/v1/external/search", "get", { query });')).toEqual(
      []
    );
    expect(lint('appApi.get("/v1/settings-sections");')).toEqual([]);
  });
});

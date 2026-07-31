// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectAuthorizationTransportOptions } from "../../../../eslint-rules/no-direct-authorization-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `authorization` вне транспортной границы — в обеих формах адреса.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": [
        "error",
        ...noDirectAuthorizationTransportOptions,
      ],
    },
  });

describe("гейт прямых вызовов путей authorization", () => {
  it("краснеет на полном пути с префиксом модуля", () => {
    const messages = lint(
      'authorizationApi.post("/api/authorization/v1/logout");'
    );

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/authorization");
  });

  it("краснеет на пути контракта без префикса — интерсептор допишет его сам", () => {
    expect(lint('authorizationApi.post("/v1/logout");')).toHaveLength(1);
    expect(
      lint('authorizationApi.request({ url: "/v1/logout", method: "post" });')
    ).toHaveLength(1);
    expect(lint('fetch("/api/authorization/v1/logout");')).toHaveLength(1);
  });

  it("вызов операции контракта не трогает", () => {
    expect(lint('request("/v1/logout", "post", {});')).toEqual([]);
  });

  it("браузерную навигацию на вход не трогает — это не HTTP-вызов фронта", () => {
    expect(
      lint('window.location.href = "/api/authorization/v1/fake/login?next=/";')
    ).toEqual([]);
  });
});

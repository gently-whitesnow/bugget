// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectUsersTransportOptions } from "../../../../eslint-rules/no-direct-users-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `users` вне транспортной границы.
 *
 * Тест проверяет саму красноту правила — иначе правило, которое ничего не ловит,
 * выглядит как выполненный инвариант.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": ["error", ...noDirectUsersTransportOptions],
    },
  });

describe("гейт прямых вызовов путей users", () => {
  it("краснеет на строковом пути в axios-вызове", () => {
    const messages = lint('usersApi.get("/api/users/v1/workspaces");');

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/users");
  });

  it("краснеет на template-пути с подстановкой контекста", () => {
    expect(
      lint(
        "usersApi.put(`/api/users/v1/workspaces/${workspaceId}/teams/${teamId}/users`, body);"
      )
    ).toHaveLength(1);
  });

  it("краснеет на config-form: путь в поле url", () => {
    expect(
      lint(
        'usersApi.request({ url: "/api/users/v1/workspaces", method: "get" });'
      )
    ).toHaveLength(1);
    expect(
      lint('usersApi.request({ ...config, url: "/api/users/v1/workspaces" });')
    ).toHaveLength(1);
  });

  it("краснеет на вызове самого инстанса и на обходе axios через fetch", () => {
    expect(lint('usersApi({ url: "/api/users/v1/workspaces" });')).toHaveLength(
      1
    );
    expect(
      lint('fetch("/api/users/v1/workspaces/1/teams/2/users/avatar");')
    ).toHaveLength(1);
  });

  it("вызов операции контракта не трогает", () => {
    expect(
      lint(
        'request("/v1/workspaces/{workspaceId}/teams/{teamId}/users", "get", { path });'
      )
    ).toEqual([]);
  });

  it("пути других модулей не трогает", () => {
    expect(lint('appApi.get("/v2/reports");')).toEqual([]);
    expect(
      lint('authorizationApi.post("/api/authorization/v1/logout");')
    ).toEqual([]);
  });
});

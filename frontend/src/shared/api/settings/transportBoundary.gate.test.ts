// @vitest-environment node
import { describe, expect, it } from "vitest";
import { Linter } from "eslint";
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore -- конфиг линтера лежит вне src и в tsconfig проекта не входит
import { noDirectSettingsTransportOptions } from "../../../../eslint-rules/no-direct-settings-transport.js";

/**
 * Гейт, а не текст карточки: `frontend-lint` краснеет на новом прямом вызове
 * пути модуля `settings` вне транспортной границы.
 */

const linter = new Linter();

const lint = (code: string) =>
  linter.verify(code, {
    rules: {
      "no-restricted-syntax": ["error", ...noDirectSettingsTransportOptions],
    },
  });

describe("гейт прямых вызовов путей settings", () => {
  it("краснеет на списке секций", () => {
    const messages = lint('appApi.get("/v1/settings-sections");');

    expect(messages).toHaveLength(1);
    expect(messages[0].message).toContain("src/shared/api/settings");
  });

  it("краснеет на всех трёх уровнях обновления настройки", () => {
    expect(
      lint(
        "appApi.put(`/v1/workspace-settings-sections/${sectionId}/settings/${settingId}`, values);"
      )
    ).toHaveLength(1);
    expect(
      lint(
        "appApi.put(`/v1/team-settings-sections/${sectionId}/settings/${settingId}`, values);"
      )
    ).toHaveLength(1);
    expect(
      lint(
        "appApi.put(`/v1/user-settings-sections/${sectionId}/settings/${settingId}`, values);"
      )
    ).toHaveLength(1);
  });

  it("краснеет на config-form — той самой форме, которой ходил module-local дескриптор", () => {
    expect(
      lint(
        'appApi.request({ method, url: "/v1/workspace-settings-sections/a/settings/b", data });'
      )
    ).toHaveLength(1);
    expect(lint('fetch("/v1/settings-sections");')).toHaveLength(1);
  });

  it("вызов операции контракта и пути других модулей не трогает", () => {
    expect(lint('request("/v1/settings-sections", "get", {});')).toEqual([]);
    expect(lint('appApi.get("/v1/external/kaiten/boards");')).toEqual([]);
  });
});

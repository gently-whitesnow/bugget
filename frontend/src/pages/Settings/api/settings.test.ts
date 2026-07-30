import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  AxiosError,
  type AxiosAdapter,
  type InternalAxiosRequestConfig,
} from "axios";
import { appApi, parseApiError, setAppContext } from "@/shared/api";
import type { components, operations } from "@/shared/api/generated/settings";
import type { Camelized } from "@/shared/lib/types";
import {
  fetchSettingsSections,
  updateTeamSetting,
  updateUserSetting,
  updateWorkspaceSetting,
} from "./settings";
import type {
  SettingValues,
  SettingView,
  SettingsSectionsResponse,
} from "./contracts";

/**
 * Граница модуля `settings`: URL, тело запроса и разбор ответа проверяются на том же
 * инстансе `appApi`, которым ходит приложение, — с интерсепторами и без сети.
 *
 * Фикстура объявлена типом схемы из контракта, поэтому провод здесь не выдуман:
 * лишний или потерянный ключ — ошибка компиляции в гейте `frontend-typecheck`.
 */

/**
 * Ответ `GET /v1/settings-sections` ровно в том виде, в каком он приходит по
 * проводу — snake_case, форма `SettingsSections`.
 */
const wireSections: components["schemas"]["SettingsSections"] = {
  workspace_sections: [
    {
      id: "kaiten",
      title: "Kaiten",
      settings: [
        {
          id: "kaiten_url",
          title: "Адрес Kaiten",
          description: "Базовый адрес пространства",
          is_array: false,
          is_bool: false,
          values: ["https://kaiten.example"],
        },
        {
          // Настройка без пояснения: ключ на проводе есть, значение — null.
          id: "kaiten_boards",
          title: "Доски",
          description: null,
          is_array: true,
          is_bool: false,
          values: [],
        },
      ],
    },
  ],
  team_sections: [
    {
      id: "notifications",
      title: "Уведомления",
      settings: [
        {
          id: "notify_on_new_bug",
          title: "Сообщать о новых багах",
          description: null,
          is_array: false,
          is_bool: true,
          values: ["true"],
        },
      ],
    },
  ],
  // Пустая секция уровня пользователя — коллекция обязана дожить до UI как [].
  user_sections: [],
};

/** Ответ PUT-ручек: одна обновлённая настройка, snake_case. */
const wireSetting: components["schemas"]["Setting"] = {
  id: "kaiten_url",
  title: "Адрес Kaiten",
  description: null,
  is_array: false,
  is_bool: false,
  values: ["https://kaiten.example/new"],
};

const originalAdapter = appApi.defaults.adapter;
let sent: InternalAxiosRequestConfig[] = [];

const respondWith = (data: unknown) => {
  const adapter: AxiosAdapter = async (config) => {
    sent.push(config);
    return {
      data,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  };
  appApi.defaults.adapter = adapter;
};

const respondWithProblem = (status: number, data: unknown) => {
  const adapter: AxiosAdapter = async (config) => {
    sent.push(config);
    throw new AxiosError(
      "Request failed",
      AxiosError.ERR_BAD_REQUEST,
      config,
      {} as never,
      {
        data,
        status,
        statusText: "",
        headers: { "content-type": "application/problem+json" },
        config,
      } as never
    );
  };
  appApi.defaults.adapter = adapter;
};

const lastRequest = () => sent[sent.length - 1];

beforeEach(() => {
  sent = [];
  // Контекст задаётся явно: путь ручки собирает интерсептор appApi.
  setAppContext(7, 11);
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
});

describe("чтение секций настроек", () => {
  it("зовёт публичный URL и отдаёт ответ в camelCase", async () => {
    respondWith(wireSections);

    const sections = await fetchSettingsSections();

    expect(lastRequest().method).toBe("get");
    expect(lastRequest().url).toBe(
      "/api/app/workspaces/7/teams/11/v1/settings-sections"
    );

    const [section] = sections.workspaceSections;
    expect(section.id).toBe("kaiten");
    const [url, boards] = section.settings;
    expect(url.isArray).toBe(false);
    expect(url.isBool).toBe(false);
    expect(url.description).toBe("Базовый адрес пространства");
    // nullable-описание доезжает до UI как null, а не теряется.
    expect(boards.description).toBeNull();
    expect(boards.isArray).toBe(true);
    expect(boards.values).toEqual([]);

    expect(sections.teamSections[0].settings[0].isBool).toBe(true);
    expect(sections.userSections).toEqual([]);
  });

  it("в ответе не остаётся wire-имён", async () => {
    respondWith(wireSections);

    const sections = await fetchSettingsSections();

    expect(sections).not.toHaveProperty("workspace_sections");
    expect(sections.workspaceSections[0].settings[0]).not.toHaveProperty(
      "is_array"
    );
  });
});

describe("обновление настройки", () => {
  const cases = [
    {
      name: "workspace",
      call: updateWorkspaceSetting,
      path: "/api/app/workspaces/7/teams/11/v1/workspace-settings-sections/kaiten/settings/kaiten_url",
    },
    {
      name: "team",
      call: updateTeamSetting,
      path: "/api/app/workspaces/7/teams/11/v1/team-settings-sections/kaiten/settings/kaiten_url",
    },
    {
      name: "user",
      call: updateUserSetting,
      path: "/api/app/workspaces/7/teams/11/v1/user-settings-sections/kaiten/settings/kaiten_url",
    },
  ] as const;

  it.each(cases)(
    "$name: PUT по своему URL с массивом значений в теле",
    async ({ call, path }) => {
      respondWith(wireSetting);

      const updated = await call("kaiten", "kaiten_url", [
        "https://kaiten.example/new",
      ]);

      expect(lastRequest().method).toBe("put");
      expect(lastRequest().url).toBe(path);
      // Тело — голый массив: интерсептор запроса ничего в нём не переименовывает.
      expect(JSON.parse(lastRequest().data as string)).toEqual([
        "https://kaiten.example/new",
      ]);
      expect(updated.values).toEqual(["https://kaiten.example/new"]);
      expect(updated.isBool).toBe(false);
      expect(updated.description).toBeNull();
    }
  );

  it("ошибку контракта отдаёт вызывающему — на ней модель показывает уведомление", async () => {
    respondWithProblem(404, {
      type: "urn:bugget:error:setting_not_found",
      title: "Настройка не найдена",
      status: 404,
      code: "setting_not_found",
      traceId: "trace-1",
    });

    const failure = await updateUserSetting("kaiten", "нет-такой", ["x"]).then(
      () => null,
      (error: unknown) => error
    );

    expect(failure).toBeInstanceOf(AxiosError);
    expect(parseApiError(failure).code).toBe("setting_not_found");
  });
});

/*
 * Ниже — проверки уровня типов: их держит `tsc --noEmit` в гейте frontend-typecheck.
 */

/** Строгое равенство типов: при расхождении `false` не присвоится `true`. */
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;

type Json200<O extends keyof operations> = Camelized<
  operations[O]["responses"][200]["content"]["application/json"]
>;

// Формы, которые читает UI, — ровно ответы операций контракта, без второго DTO.
const sectionsMatchOperation: Equal<
  SettingsSectionsResponse,
  Json200<"Settings_GetSettingsSections">
> = true;
const workspaceUpdateMatchesOperation: Equal<
  SettingView,
  Json200<"Settings_UpdateWorkspaceSetting">
> = true;
const teamUpdateMatchesOperation: Equal<
  SettingView,
  Json200<"Settings_UpdateTeamSetting">
> = true;
const userUpdateMatchesOperation: Equal<
  SettingView,
  Json200<"Settings_UpdateUserSetting">
> = true;

// Тело всех трёх PUT — один и тот же `SettingValues` контракта.
const workspaceBodyMatchesOperation: Equal<
  SettingValues,
  operations["Settings_UpdateWorkspaceSetting"]["requestBody"]["content"]["application/json"]
> = true;
const teamBodyMatchesOperation: Equal<
  SettingValues,
  operations["Settings_UpdateTeamSetting"]["requestBody"]["content"]["application/json"]
> = true;
const userBodyMatchesOperation: Equal<
  SettingValues,
  operations["Settings_UpdateUserSetting"]["requestBody"]["content"]["application/json"]
> = true;

// @ts-expect-error wire-имени в коде фронта нет: до UI доезжает camelCase
const readWireKey = (setting: SettingView) => setting.is_array;

const readDescriptionAsString = (setting: SettingView): string =>
  // @ts-expect-error `description` nullable — присвоить его `string` нельзя
  setting.description;

describe("типизированная граница контракта", () => {
  it("формы выведены из операций settings", () => {
    expect([
      sectionsMatchOperation,
      workspaceUpdateMatchesOperation,
      teamUpdateMatchesOperation,
      userUpdateMatchesOperation,
      workspaceBodyMatchesOperation,
      teamBodyMatchesOperation,
      userBodyMatchesOperation,
    ]).toEqual([true, true, true, true, true, true, true]);

    // Обращения ниже существуют только ради @ts-expect-error выше: до UI доезжает
    // уже сконвертированная форма, wire-имён в ней нет.
    const setting: SettingView = {
      id: wireSetting.id,
      title: wireSetting.title,
      description: wireSetting.description,
      isArray: wireSetting.is_array,
      isBool: wireSetting.is_bool,
      values: wireSetting.values,
    };
    expect(readWireKey(setting)).toBeUndefined();
    expect(readDescriptionAsString(setting)).toBeNull();
  });
});

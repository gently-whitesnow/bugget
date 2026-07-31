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
import type { SettingResult, SettingValuesBody } from "./settings";

/**
 * Провод модуля `settings` после переезда на общую границу
 * `shared/api/operation.ts`.
 *
 * Механика вызова сменилась, публичный контракт — нет: тест смотрит на то, что
 * реально уходит в сеть (URL, метод, тело) и что доезжает до UI. Фикстуры
 * объявлены типами схем контракта, поэтому провод здесь не выдуман: лишний или
 * потерянный ключ — ошибка компиляции в гейте `frontend-typecheck`.
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

/** Префикс, который навешивает интерсептор appApi на путь модуля. */
const contextPrefix = "/api/app/workspaces/7/teams/11";

beforeEach(() => {
  sent = [];
  // Контекст задаётся явно: путь ручки собирает интерсептор appApi.
  setAppContext(7, 11);
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
});

describe("чтение секций настроек", () => {
  it("зовёт прежний публичный URL и отдаёт ответ в camelCase", async () => {
    respondWith(wireSections);

    const sections = await fetchSettingsSections();

    expect(lastRequest().method).toBe("get");
    expect(lastRequest().url).toBe(`${contextPrefix}/v1/settings-sections`);

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

  it("ручка без query уходит без хвостового «?»", async () => {
    respondWith(wireSections);

    await fetchSettingsSections();

    expect(lastRequest().url).not.toContain("?");
  });
});

describe("обновление настройки", () => {
  const cases = [
    {
      name: "workspace",
      call: updateWorkspaceSetting,
      path: "/v1/workspace-settings-sections/kaiten/settings/kaiten_url",
    },
    {
      name: "team",
      call: updateTeamSetting,
      path: "/v1/team-settings-sections/kaiten/settings/kaiten_url",
    },
    {
      name: "user",
      call: updateUserSetting,
      path: "/v1/user-settings-sections/kaiten/settings/kaiten_url",
    },
  ] as const;

  it.each(cases)(
    "$name: PUT по прежнему адресу с массивом значений в теле",
    async ({ call, path }) => {
      respondWith(wireSetting);

      const updated = await call("kaiten", "kaiten_url", [
        "https://kaiten.example/new",
      ]);

      expect(lastRequest().method).toBe("put");
      expect(lastRequest().url).toBe(contextPrefix + path);
      // Тело — голый массив: интерсептор запроса ничего в нём не переименовывает.
      expect(JSON.parse(lastRequest().data as string)).toEqual([
        "https://kaiten.example/new",
      ]);
      expect(updated.values).toEqual(["https://kaiten.example/new"]);
      expect(updated.isBool).toBe(false);
      expect(updated.description).toBeNull();
    }
  );

  it.each(cases)(
    "$name: не теряет пустой массив, bool и вырожденные строки",
    async ({ call }) => {
      respondWith(wireSetting);

      const bodies = [
        [],
        ["false"],
        ["", "0", "null", "  ", "значение"],
      ] satisfies SettingValuesBody[];

      for (const body of bodies) {
        await call("kaiten", "setting", body);
      }

      expect(sent).toHaveLength(bodies.length);
      expect(sent.map(({ data }) => JSON.parse(data as string))).toEqual(
        bodies
      );
    }
  );

  it("сегменты пути подставляются дословно, без экранирования", async () => {
    respondWith(wireSetting);

    await updateUserSetting("kaiten", "kaiten_url space", ["x"]);

    expect(lastRequest().url).toBe(
      `${contextPrefix}/v1/user-settings-sections/kaiten/settings/kaiten_url space`
    );
  });

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

  it("после ошибки повторно отправляет тот же запрос и возвращает успех", async () => {
    respondWithProblem(500, {
      type: "urn:bugget:error:internal",
      title: "Внутренняя ошибка",
      status: 500,
      code: "internal",
      traceId: "trace-retry",
    });

    await expect(
      updateWorkspaceSetting("kaiten", "kaiten_url", ["new"])
    ).rejects.toBeInstanceOf(AxiosError);

    respondWith(wireSetting);
    const updated = await updateWorkspaceSetting("kaiten", "kaiten_url", [
      "new",
    ]);

    expect(sent).toHaveLength(2);
    expect(sent[0].method).toBe("put");
    expect(sent[1].method).toBe("put");
    expect(sent[1].url).toBe(sent[0].url);
    expect(JSON.parse(sent[1].data as string)).toEqual(["new"]);
    expect(updated).toMatchObject({
      description: null,
      isArray: false,
      isBool: false,
    });
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

/*
 * Сигнатуры ручек выведены из операций контракта — второго представления тела,
 * сегментов и ответа рядом нет. Раньше это доказывалось через module-local
 * дескриптор (`settingsRoutes`, `SettingsMethod`); теперь связь «путь + метод →
 * операция» держит общая граница, и проверять остаётся сами ручки.
 */
const sectionsReturnsOperationResponse: Equal<
  Awaited<ReturnType<typeof fetchSettingsSections>>,
  Json200<"Settings_GetSettingsSections">
> = true;
const workspaceTakesOperationBody: Equal<
  Parameters<typeof updateWorkspaceSetting>[2],
  operations["Settings_UpdateWorkspaceSetting"]["requestBody"]["content"]["application/json"]
> = true;
const workspaceReturnsOperationResponse: Equal<
  Awaited<ReturnType<typeof updateWorkspaceSetting>>,
  Json200<"Settings_UpdateWorkspaceSetting">
> = true;
const teamReturnsOperationResponse: Equal<
  Awaited<ReturnType<typeof updateTeamSetting>>,
  Json200<"Settings_UpdateTeamSetting">
> = true;
const userReturnsOperationResponse: Equal<
  Awaited<ReturnType<typeof updateUserSetting>>,
  Json200<"Settings_UpdateUserSetting">
> = true;
const bodyMatchesOperation: Equal<
  SettingValuesBody,
  operations["Settings_UpdateWorkspaceSetting"]["requestBody"]["content"]["application/json"]
> = true;

// @ts-expect-error wire-имени в коде фронта нет: до UI доезжает camelCase
const readWireKey = (setting: SettingResult) => setting.is_array;

const readDescriptionAsString = (setting: SettingResult): string =>
  // @ts-expect-error `description` nullable — присвоить его `string` нельзя
  setting.description;

describe("типизированная граница контракта", () => {
  it("сигнатуры ручек выведены из операций контракта", () => {
    // Равенства держит `tsc --noEmit` (гейт frontend-typecheck); тест фиксирует намерение.
    expect([
      sectionsReturnsOperationResponse,
      workspaceTakesOperationBody,
      workspaceReturnsOperationResponse,
      teamReturnsOperationResponse,
      userReturnsOperationResponse,
      bodyMatchesOperation,
    ]).toEqual(Array(6).fill(true));
  });

  it("до UI доезжает camelCase-форма ответа", () => {
    const setting: SettingResult = {
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

// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { setAppContext, usersApi } from "@/shared/api/instances";
import {
  autocompleteUsers,
  fetchUsers,
  getUser,
  linkMattermost,
  listExternalLinks,
  listUsers,
  listUsersInContext,
  mergeUsers,
  unlinkMattermost,
  unlinkProvider,
  updateUserInContext,
} from "./users";
import { deleteAvatar, resolveAvatarUrl, uploadAvatar } from "./avatar";
import { createTeam, deleteTeam, teamsAutocomplete, updateTeam } from "./teams";
import {
  deleteTeamMember,
  joinTeam,
  leaveTeam,
  listTeamMembers,
} from "./teamMembers";
import {
  createWorkspace,
  deleteWorkspace,
  joinWorkspace,
  listWorkspacesContext,
  renameWorkspace,
} from "./workspaces";

/**
 * Провод модуля `users` после перевода на сгенерированный контракт.
 *
 * Типы выведены из yaml, но адрес, имена query, форма тела и multipart обязаны
 * остаться прежними 1:1 — фронт стоит в проде у заказчика. Поэтому тест смотрит
 * не на типы (их держит `tsc`), а на то, что реально уходит в сеть: подменяем
 * транспорт и читаем собранный запрос.
 *
 * Отдельная тема — две публичные формы адреса. Короткую (`workspaceId`/`teamId`
 * аргументами) и контекстную (те же сегменты из `getAppContext`) проверяем
 * рядом, включая поведение контекстной формы при незаданном контексте: адрес без
 * сегмента и предупреждение в консоль — ровно то, что делал `usersPathWithContext`.
 */

const prefix = "/api/users/v1";
const contextPrefix = `${prefix}/workspaces/1/teams/2`;

let captured: InternalAxiosRequestConfig | null = null;
let payload: unknown = null;
let responseStatus = 200;
let responseContentType = "application/json";
let originalAdapter: AxiosAdapter | undefined;

const sent = () => {
  if (!captured) throw new Error("Запрос не был отправлен");
  return captured;
};

/** Тело на уровне транспорта — уже сериализованный JSON. */
const sentJsonBody = () => JSON.parse(sent().data as string);

beforeEach(() => {
  setAppContext(1, 2);
  captured = null;
  payload = null;
  responseStatus = 200;
  responseContentType = "application/json";
  originalAdapter = usersApi.defaults.adapter as AxiosAdapter | undefined;
  usersApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: payload,
      status: responseStatus,
      statusText: responseStatus === 204 ? "No Content" : "OK",
      headers: { "content-type": responseContentType },
      config,
    };
  }) as AxiosAdapter;
});

afterEach(() => {
  usersApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
  vi.restoreAllMocks();
});

describe("короткая форма адреса: контекст приходит аргументами", () => {
  it("профиль запрашивается по пути с рабочим пространством и командой", async () => {
    await getUser(1, 2);

    expect(sent().url).toBe(`${contextPrefix}/users`);
    expect(sent().method).toBe("get");
  });

  it("не готовый контекст доезжает до адреса как раньше, а не падает", async () => {
    await getUser(undefined, undefined);

    expect(sent().url).toBe(
      `${prefix}/workspaces/undefined/teams/undefined/users`
    );
  });

  it("пользователи по списку: идентификаторы строками в теле", async () => {
    payload = [];
    await listUsers(1, 2, [3, "4"]);

    expect(sent().url).toBe(`${contextPrefix}/users/batch/list`);
    expect(sent().method).toBe("post");
    expect(sentJsonBody()).toEqual(["3", "4"]);
  });

  it("участники команды, удаление участника, выход и вступление", async () => {
    await listTeamMembers(1, 2);
    expect(sent().url).toBe(`${contextPrefix}/members`);

    await deleteTeamMember(1, 2, 7);
    expect(sent().url).toBe(`${contextPrefix}/members/7`);
    expect(sent().method).toBe("delete");

    await leaveTeam(1, 2);
    expect(sent().url).toBe(`${contextPrefix}/members`);
    expect(sent().method).toBe("delete");

    await joinTeam(1, 2);
    expect(sent().url).toBe(`${contextPrefix}/members/join`);
    expect(sent().method).toBe("post");
  });

  it("удаление участника сохраняет строковый long-идентификатор без потери точности", async () => {
    const userId = "9007199254740993";

    await deleteTeamMember(1, 2, userId);

    expect(sent().url).toBe(`${contextPrefix}/members/${userId}`);
    expect(sent().method).toBe("delete");
  });

  it("команды: создание, переименование, удаление и подсказки", async () => {
    await createTeam(1, "Команда");
    expect(sent().url).toBe(`${prefix}/workspaces/1/teams`);
    expect(sentJsonBody()).toEqual({ name: "Команда" });

    await updateTeam(1, 2, "Другая");
    expect(sent().url).toBe(`${prefix}/workspaces/1/teams/2`);
    expect(sent().method).toBe("put");

    await deleteTeam(1, 2);
    expect(sent().url).toBe(`${prefix}/workspaces/1/teams/2`);
    expect(sent().method).toBe("delete");

    payload = { teams: [], total: 0 };
    await teamsAutocomplete("ко");
    expect(sent().url).toBe(
      `${prefix}/workspaces/1/teams/autocomplete?searchString=%D0%BA%D0%BE&skip=0&take=10`
    );
  });

  it("рабочие пространства: контекст, создание, переименование, удаление, вступление", async () => {
    await listWorkspacesContext();
    expect(sent().url).toBe(`${prefix}/workspaces`);

    await createWorkspace({ name: "Пространство" });
    expect(sent().url).toBe(`${prefix}/workspaces`);
    expect(sentJsonBody()).toEqual({ name: "Пространство" });

    await renameWorkspace(1, "Новое");
    expect(sent().url).toBe(`${prefix}/workspaces/1`);
    expect(sent().method).toBe("put");

    await deleteWorkspace(1);
    expect(sent().url).toBe(`${prefix}/workspaces/1`);
    expect(sent().method).toBe("delete");

    await joinWorkspace(1);
    expect(sent().url).toBe(`${prefix}/workspaces/1/members/join`);
  });

  it("пустой 204-ответ не превращается в объект", async () => {
    responseStatus = 204;
    responseContentType = "";
    payload = undefined;

    await expect(deleteTeam(1, 2)).resolves.toBeUndefined();
    expect(sent().url).toBe(`${prefix}/workspaces/1/teams/2`);
    expect(sent().method).toBe("delete");
  });

  it("пустые и null-коллекции bootstrap-ответа сохраняются", async () => {
    payload = {
      workspaces: [],
      teams_member: null,
      workspaces_member: null,
    };

    await expect(listWorkspacesContext()).resolves.toEqual({
      workspaces: [],
      teamsMember: null,
      workspacesMember: null,
    });
  });

  it("ошибка self-hosted join пробрасывается с исходным problem+json", async () => {
    const problem = {
      type: "about:blank",
      title: "Недостаточно прав",
      status: 403,
      errors: { workspace_id: ["Недоступно"] },
    };
    const error = Object.assign(
      new Error("Request failed with status code 403"),
      {
        config: {},
        response: {
          data: problem,
          status: 403,
          statusText: "Forbidden",
          headers: { "content-type": "application/problem+json" },
          config: {},
        },
      }
    );
    usersApi.defaults.adapter = (async (config) => {
      captured = config;
      error.config = config;
      error.response.config = config;
      throw error;
    }) as AxiosAdapter;

    await expect(joinTeam(1, 2)).rejects.toBe(error);
    expect(sent().url).toBe(`${contextPrefix}/members/join`);
    expect(error.response.data).toEqual(problem);
    expect(error.response.data.errors).toHaveProperty("workspace_id");
  });
});

describe("контекстная форма адреса: контекст из приложения", () => {
  it("подсказки по пользователям: имена query из контракта", async () => {
    payload = { users: [], total: 0 };
    await autocompleteUsers({ searchString: "an", skip: 0, take: 10 });

    expect(sent().url).toBe(
      `${contextPrefix}/users/autocomplete?searchString=an&skip=0&take=10`
    );
  });

  it("обновление профиля уходит тем же телом", async () => {
    await updateUserInContext({ name: "Имя" });

    expect(sent().url).toBe(`${contextPrefix}/users`);
    expect(sent().method).toBe("put");
    expect(sentJsonBody()).toEqual({ name: "Имя" });
  });

  it("способы входа: список, отвязка провайдера в пути и объединение аккаунтов", async () => {
    payload = [];
    await listExternalLinks();
    expect(sent().url).toBe(`${contextPrefix}/users/external-links`);

    await unlinkProvider("mattermost");
    expect(sent().url).toBe(`${contextPrefix}/users/external-links/mattermost`);
    expect(sent().method).toBe("delete");

    await mergeUsers({ sourceUserId: "u-1" });
    expect(sent().url).toBe(`${contextPrefix}/users/merge`);
    // Тело на проводе snake_case: конверсию делает интерсептор, имя приходит
    // из схемы MergeUsersRequest.
    expect(sentJsonBody()).toEqual({ source_user_id: "u-1" });
  });

  it("Mattermost: привязка телом контракта и отвязка", async () => {
    await linkMattermost({ mattermostUserId: "m-1" });
    expect(sent().url).toBe(`${contextPrefix}/users/mattermost`);
    expect(sentJsonBody()).toEqual({ mattermost_user_id: "m-1" });

    await unlinkMattermost();
    expect(sent().method).toBe("delete");
  });

  it("пользователи по списку в контекстной форме", async () => {
    payload = [];
    await listUsersInContext(["u-1"]);

    expect(sent().url).toBe(`${contextPrefix}/users/batch/list`);
    expect(sentJsonBody()).toEqual(["u-1"]);
  });

  it("пустой список пользователей не ходит в сеть", async () => {
    expect(await fetchUsers([])).toEqual([]);
    expect(captured).toBeNull();
  });

  it("незаданный контекст: адрес без сегмента и предупреждение", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    setAppContext(null, null);
    payload = [];

    await listExternalLinks();

    expect(sent().url).toBe(`${prefix}/users/external-links`);
    expect(warn).toHaveBeenCalled();
  });
});

describe("multipart: загрузка аватара", () => {
  it("имя поля из схемы, заголовок multipart и адрес контекстной формы", async () => {
    const file = new File(["avatar"], "avatar.png", { type: "image/png" });

    await uploadAvatar(file);

    expect(sent().url).toBe(`${contextPrefix}/users/avatar`);
    expect(sent().method).toBe("post");
    expect(sent().headers["Content-Type"]).toBe("multipart/form-data");

    const body = sent().data as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect(body.get("file")).toBe(file);
  });

  it("удаление аватара уходит без тела", async () => {
    await deleteAvatar();

    expect(sent().url).toBe(`${contextPrefix}/users/avatar`);
    expect(sent().method).toBe("delete");
    expect(sent().data).toBeUndefined();
  });
});

describe("ссылка на содержимое аватара", () => {
  it("чужой аватар: адрес ручки с идентификатором и ключ файла в v=", () => {
    expect(resolveAvatarUrl("7", "avatars/7.png")).toBe(
      `${contextPrefix}/users/7/avatar/content?v=avatars%2F7.png`
    );
  });

  it("свой аватар берётся у отдельной ручки", () => {
    expect(
      resolveAvatarUrl("7", "avatars/7.png", { useCurrentUserEndpoint: true })
    ).toBe(`${contextPrefix}/users/avatar/content?v=avatars%2F7.png`);
  });

  it("внешняя ссылка отдаётся как есть, пустой ключ — это отсутствие аватара", () => {
    expect(resolveAvatarUrl("7", "https://cdn/x.png")).toBe(
      "https://cdn/x.png"
    );
    expect(resolveAvatarUrl("7", null)).toBeNull();
  });

  it("без контекста адрес теряет сегмент, как и раньше", () => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
    setAppContext(null, null);

    expect(resolveAvatarUrl("7", "avatars/7.png")).toBe(
      `${prefix}/users/7/avatar/content?v=avatars%2F7.png`
    );
  });
});

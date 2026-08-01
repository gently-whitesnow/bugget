// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { appApi, reportsApi, setAppContext } from "@/shared/api";
import { analyticsApi } from "@/shared/api";
import {
  createReport,
  fetchReport,
  fetchReportsList,
  patchReport,
  resolveLegacyReport,
} from "./reports";
import { createBug, updateBug } from "./bugs";
import {
  createBugStep,
  deleteBugStep,
  patchBugStep,
  updateBugStepsOrder,
} from "./bugSteps";
import { createComment, deleteComment, updateComment } from "./comments";
import {
  createReportLink,
  deleteReportLink,
  updateReportLink,
} from "./reportLinks";
import {
  createBugStepAttachment,
  createCommentAttachment,
  deleteBugAttachment,
  deleteBugStepAttachment,
  deleteCommentAttachment,
  renameBugAttachment,
  renameBugStepAttachment,
  renameCommentAttachment,
  uploadAttachment,
} from "./attachments";

/**
 * Провод модуля `reports` после перевода на сгенерированный контракт.
 *
 * Типы теперь выведены из yaml, но URL, имена query, форма тела и multipart
 * обязаны остаться прежними 1:1 — фронт стоит в проде у заказчика. Поэтому тест
 * смотрит не на типы (их держит `tsc`), а на то, что реально уходит в сеть:
 * подменяем транспорт и читаем собранный запрос.
 */

const contextPrefix = "/api/app/workspaces/1/teams/2";

let captured: InternalAxiosRequestConfig | null = null;
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
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: null,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
});

describe("пути репортов", () => {
  it("карточка репорта запрашивается по alias в пути", async () => {
    await fetchReport("team-42");

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42`);
    expect(sent().method).toBe("get");
  });

  it("список: имена query из контракта, статусы повторяющимся ключом", async () => {
    await fetchReportsList("u-1", "t-1", ["backlog", "fix"], 20, 10);

    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports?userId=u-1&teamId=t-1&reportStatuses=backlog&reportStatuses=fix&skip=20&take=10`
    );
  });

  it("список без фильтров не отправляет пустые userId и teamId", async () => {
    await fetchReportsList(null, null, null, 0, 10);

    expect(sent().url).toBe(`${contextPrefix}/v2/reports?skip=0&take=10`);
  });

  it("legacy-идентификатор разрешается по прежнему адресу", async () => {
    await resolveLegacyReport("42");

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/legacy/42`);
  });

  it("legacy-идентификатор с ведущими нулями сохраняет их в адресе", async () => {
    await resolveLegacyReport("00042");

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/legacy/00042`);
  });

  it("нечисловой legacy-сегмент уходит как есть, а не превращается в NaN", async () => {
    await resolveLegacyReport("abc");

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/legacy/abc`);
    expect(sent().url).not.toContain("NaN");
  });

  it("аналитика репорта остаётся sub-resource репорта", async () => {
    await analyticsApi.getReportAnalytics(7);

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/7/analytics`);
    expect(sent().method).toBe("get");
  });

  it("удаление ссылки — DELETE без тела и без хвостового «?»", async () => {
    await deleteReportLink("team-42", 11);

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/links/11`);
    expect(sent().method).toBe("delete");
    expect(sent().data).toBeUndefined();
  });

  it("поиск сохраняет все query-параметры, массивы и пустые значения", async () => {
    await reportsApi.searchReports({
      query: "",
      reportStatuses: ["backlog", "test"],
      userId: "u-1",
      teamId: "t-1",
      sort: "created_at",
      skip: 0,
      take: 0,
      creatorTypes: ["user", "system"],
    });

    expect(sent().url).toBe(
      `${contextPrefix}/v1/reports/search?query=&reportStatuses=backlog&reportStatuses=test&userId=u-1&teamId=t-1&sort=created_at&skip=0&take=0&creatorTypes=user&creatorTypes=system`
    );
  });
});

describe("тела запросов уходят в snake_case контракта", () => {
  it("создание репорта: POST и title", async () => {
    let requestCount = 0;
    appApi.defaults.adapter = (async (config) => {
      requestCount += 1;
      captured = config;
      return {
        data: { id: "body-alias", title: "Новый репорт" },
        status: 201,
        statusText: "Created",
        headers: {
          "content-type": "application/json",
          location: "https://other.example/v2/reports/header-alias",
        },
        config,
      };
    }) as AxiosAdapter;

    const report = await createReport({ title: "Новый репорт" });

    expect(sent().method).toBe("post");
    expect(sent().url).toBe(`${contextPrefix}/v2/reports`);
    expect(sentJsonBody()).toEqual({ title: "Новый репорт" });
    expect(report).toEqual({ id: "body-alias", title: "Новый репорт" });
    expect(requestCount).toBe(1);
  });

  it("PATCH репорта: responsible_user_id и is_excluded_from_analytics", async () => {
    await patchReport("team-42", {
      responsibleUserId: "u-9",
      isExcludedFromAnalytics: true,
    });

    expect(sent().method).toBe("patch");
    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42`);
    expect(sentJsonBody()).toEqual({
      responsible_user_id: "u-9",
      is_excluded_from_analytics: true,
    });
  });

  it("создание бага: receive и expect как есть", async () => {
    await createBug("team-42", { receive: "падает", expect: null });

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/bugs`);
    expect(sentJsonBody()).toEqual({ receive: "падает", expect: null });
  });

  it("PATCH бага: статус и текстовые поля", async () => {
    await updateBug("team-42", 7, {
      status: "verified",
      expect: "не падает",
    });

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/bugs/7`);
    expect(sentJsonBody()).toEqual({
      status: "verified",
      expect: "не падает",
    });
  });

  it("порядок шагов: stepIds в коде — step_ids на проводе", async () => {
    await updateBugStepsOrder("team-42", 7, { stepIds: [3, 1, 2] });

    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/steps/order`
    );
    expect(sent().method).toBe("put");
    expect(sentJsonBody()).toEqual({ step_ids: [3, 1, 2] });
  });

  it("комментарий: text и audience", async () => {
    await createComment("team-42", 7, {
      text: "воспроизвёл",
      audience: "external",
    });

    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/comments`
    );
    expect(sentJsonBody()).toEqual({
      text: "воспроизвёл",
      audience: "external",
    });
  });

  it("ссылка репорта: link и name", async () => {
    await createReportLink("team-42", { link: "https://ati.su", name: "ATI" });

    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/links`);
    expect(sentJsonBody()).toEqual({ link: "https://ati.su", name: "ATI" });
  });

  it("обновление ссылки: PUT на конкретную ссылку", async () => {
    await updateReportLink("team-42", 11, {
      link: "https://example.test/new",
      name: "Новая",
    });

    expect(sent().method).toBe("put");
    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/links/11`);
    expect(sentJsonBody()).toEqual({
      link: "https://example.test/new",
      name: "Новая",
    });
  });

  it("создание и PATCH шага используют точные методы и путь", async () => {
    await createBugStep("team-42", 7, { text: "открыть страницу" });
    expect(sent().method).toBe("post");
    expect(sent().url).toBe(`${contextPrefix}/v2/reports/team-42/bugs/7/steps`);
    expect(sentJsonBody()).toEqual({ text: "открыть страницу" });

    await patchBugStep("team-42", 7, 3, { text: "обновлённый шаг" });
    expect(sent().method).toBe("patch");
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/steps/3`
    );
    expect(sentJsonBody()).toEqual({ text: "обновлённый шаг" });
  });

  it("обновление комментария: PUT, nullable audience сохраняется", async () => {
    await updateComment("team-42", 7, 5, {
      text: "уточнение",
      audience: null,
    });

    expect(sent().method).toBe("put");
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/comments/5`
    );
    expect(sentJsonBody()).toEqual({ text: "уточнение", audience: null });
  });

  it("батч-счётчики сохраняют вложенные snake_case-поля и пустой scopes", async () => {
    await reportsApi.countReportsBatch({
      scopes: [
        {
          key: "My_Scope",
          statuses: [],
          teamId: null,
          creatorTypes: ["user", "system"],
        },
      ],
    });

    expect(sent().method).toBe("post");
    expect(sent().url).toBe(`${contextPrefix}/v2/reports/counts:batch`);
    expect(sentJsonBody()).toEqual({
      scopes: [
        {
          key: "My_Scope",
          statuses: [],
          team_id: null,
          creator_types: ["user", "system"],
        },
      ],
    });

    await reportsApi.countReportsBatch({ scopes: [] });
    expect(sentJsonBody()).toEqual({ scopes: [] });
  });

  it("переименование вложения: fileName в коде — file_name на проводе", async () => {
    await renameBugAttachment({
      reportId: "team-42",
      bugId: 7,
      attachmentId: 11,
      fileName: "скрин.png",
    });

    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/attachments/11`
    );
    expect(sentJsonBody()).toEqual({ file_name: "скрин.png" });
  });

  it("переименование comment/step-вложений сохраняет точные вложенные пути", async () => {
    await renameCommentAttachment({
      reportId: "team-42",
      bugId: 7,
      commentId: 5,
      attachmentId: 11,
      fileName: "comment.png",
    });
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/comments/5/attachments/11`
    );
    expect(sentJsonBody()).toEqual({ file_name: "comment.png" });

    await renameBugStepAttachment({
      reportId: "team-42",
      bugId: 7,
      stepId: 3,
      attachmentId: 12,
      fileName: "step.png",
    });
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/steps/3/attachments/12`
    );
    expect(sentJsonBody()).toEqual({ file_name: "step.png" });
  });
});

describe("загрузка вложения — multipart, а не JSON", () => {
  it.each(["fact", "expected", "comment", "bug_step"] as const)(
    "attachType=%s уходит query-параметром, файл — полем file",
    async (attachType) => {
      const file = new File(["данные"], "скрин.png", { type: "image/png" });

      await uploadAttachment({
        reportId: "team-42",
        bugId: 7,
        attachType,
        file,
      });

      expect(sent().url).toBe(
        `${contextPrefix}/v2/reports/team-42/bugs/7/attachments?attachType=${attachType}`
      );

      const body = sent().data as FormData;
      expect(body).toBeInstanceOf(FormData);
      // Имя поля берётся из схемы AttachmentUpload и конверсию регистра не проходит.
      expect(body.get("file")).toBe(file);
      expect([...body.keys()]).toEqual(["file"]);
    }
  );

  it("comment/step upload используют multipart и точные пути", async () => {
    const file = new File(["данные"], "скрин.png", { type: "image/png" });

    await createCommentAttachment("team-42", 7, 5, file);
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/comments/5/attachments`
    );
    expect(sent().method).toBe("post");
    expect((sent().data as FormData).get("file")).toBe(file);

    await createBugStepAttachment("team-42", 7, 3, file);
    expect(sent().url).toBe(
      `${contextPrefix}/v2/reports/team-42/bugs/7/steps/3/attachments`
    );
    expect(sent().method).toBe("post");
    expect((sent().data as FormData).get("file")).toBe(file);
  });
});

describe("DELETE CRUD-операций не отправляет тело и query", () => {
  it.each([
    [
      "шаг",
      () => deleteBugStep("team-42", 7, 3),
      "/v2/reports/team-42/bugs/7/steps/3",
    ],
    [
      "комментарий",
      () => deleteComment("team-42", 7, 5),
      "/v2/reports/team-42/bugs/7/comments/5",
    ],
    [
      "bug-вложение",
      () => deleteBugAttachment("team-42", 7, 11),
      "/v2/reports/team-42/bugs/7/attachments/11",
    ],
    [
      "comment-вложение",
      () => deleteCommentAttachment("team-42", 7, 5, 11),
      "/v2/reports/team-42/bugs/7/comments/5/attachments/11",
    ],
    [
      "step-вложение",
      () => deleteBugStepAttachment("team-42", 7, 3, 11),
      "/v2/reports/team-42/bugs/7/steps/3/attachments/11",
    ],
  ])("%s", async (_name, invoke, path) => {
    await invoke();

    expect(sent().method).toBe("delete");
    expect(sent().url).toBe(`${contextPrefix}${path}`);
    expect(sent().data).toBeUndefined();
  });
});

describe("ответы приходят в camelCase кода", () => {
  it("тело ответа конвертируется целиком, null/пустые массивы и attach_type 0..3 сохраняются", async () => {
    appApi.defaults.adapter = (async (config) => ({
      data: {
        id: "team-42",
        creator_team_id: "t-1",
        is_excluded_from_analytics: false,
        links: [],
        bugs: [
          {
            id: 7,
            report_id: 42,
            creator_user_id: "u-3",
            steps: null,
            comments: [],
            attachments: [0, 1, 2, 3].map((attach_type) => ({
              attach_type,
              file_name: `${attach_type}.png`,
            })),
          },
        ],
      },
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    })) as AxiosAdapter;

    const report = await fetchReport("team-42");

    expect(report.creatorTeamId).toBe("t-1");
    expect(report.isExcludedFromAnalytics).toBe(false);
    expect(report.bugs?.[0].reportId).toBe(42);
    expect(report.bugs?.[0].creatorUserId).toBe("u-3");
    expect(report.links).toEqual([]);
    expect(report.bugs?.[0].steps).toBeNull();
    expect(report.bugs?.[0].comments).toEqual([]);
    expect(
      report.bugs?.[0].attachments?.map((item) => item.attachType)
    ).toEqual([0, 1, 2, 3]);
  });
});

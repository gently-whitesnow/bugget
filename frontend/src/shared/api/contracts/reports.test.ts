import { describe, expect, it } from "vitest";
import { convertObjectToCamel } from "@/shared/lib/convertCases";
import type { components } from "@/shared/api/generated/reports";
import type { BugStatuses } from "@/shared/config";
import type {
  BugListItem,
  ListReportsResponse,
  ReportListItem,
} from "./reports";

/**
 * Ответ `GET /v2/reports` ровно в том виде, в каком он приходит по проводу —
 * snake_case, форма `ReportList` из контракта. Сверено со снимком
 * `v2.reports.list`: ни `links`, ни `bugs[].attachments`, ни `bugs[].steps`
 * в LIST больше нет.
 */
const wireListResponse: components["schemas"]["ReportList"] = {
  total: 2,
  reports: [
    {
      id: "42",
      title: "Не открывается карточка",
      status: "backlog",
      responsible_user_id: "u-1",
      past_responsible_user_id: "u-2",
      creator_user_id: "u-3",
      creator_team_id: "t-1",
      created_at: "2026-07-01T10:00:00Z",
      updated_at: "2026-07-02T10:00:00Z",
      creator_type: "user",
      is_excluded_from_analytics: false,
      participants_user_ids: ["u-1", "u-3"],
      bugs: [
        {
          id: 1,
          report_id: 42,
          title: null,
          receive: "падает",
          expect: null,
          created_at: "2026-07-01T10:00:00Z",
          updated_at: "2026-07-01T10:00:00Z",
          creator_user_id: "u-3",
          status: "open",
          creator_type: "user",
          comments: [
            {
              id: 7,
              bug_id: 1,
              text: "воспроизвёл",
              creator_user_id: "u-1",
              creator_type: "user",
              audience: "internal",
              created_at: "2026-07-01T11:00:00Z",
              updated_at: "2026-07-01T11:00:00Z",
              attachments: null,
            },
          ],
        },
      ],
    },
    {
      // Репорт без багов — пустая коллекция и `null` в creator_team_id.
      id: "43",
      title: "Пустой репорт",
      status: "test",
      responsible_user_id: "u-1",
      past_responsible_user_id: "",
      creator_user_id: "u-1",
      creator_team_id: null,
      created_at: "2026-07-03T10:00:00Z",
      updated_at: "2026-07-03T10:00:00Z",
      creator_type: "user",
      is_excluded_from_analytics: true,
      participants_user_ids: [],
      bugs: [],
    },
  ],
};

describe("формы списка репортов выведены из контракта", () => {
  it("интерсептор приводит провод к тем же ключам, что объявляет тип", () => {
    // Ровно то, что делает case-conversion интерсептор в shared/api/instances/base.ts.
    const list = convertObjectToCamel(wireListResponse) as ListReportsResponse;

    expect(list.total).toBe(2);

    const [report] = list.reports;
    expect(report.responsibleUserId).toBe("u-1");
    expect(report.pastResponsibleUserId).toBe("u-2");
    expect(report.creatorTeamId).toBe("t-1");
    expect(report.participantsUserIds).toEqual(["u-1", "u-3"]);
    expect(report.isExcludedFromAnalytics).toBe(false);
    expect(report.createdAt).toBe("2026-07-01T10:00:00Z");

    const bug = report.bugs?.[0];
    expect(bug?.reportId).toBe(42);
    expect(bug?.creatorUserId).toBe("u-3");
    // nullable-поля: заведён только receive, title и expect пришли как null
    expect(bug?.title).toBeNull();
    expect(bug?.expect).toBeNull();
    expect(bug?.comments?.[0].creatorUserId).toBe("u-1");
  });

  it("удалённых из LIST ключей нет и в данных", () => {
    const list = convertObjectToCamel(wireListResponse) as ListReportsResponse;
    const [report] = list.reports;

    expect(report).not.toHaveProperty("links");
    expect(report.bugs?.[0]).not.toHaveProperty("attachments");
    expect(report.bugs?.[0]).not.toHaveProperty("steps");
  });

  it("пустые коллекции переживают конвертацию", () => {
    const list = convertObjectToCamel(wireListResponse) as ListReportsResponse;
    const empty = list.reports[1];

    expect(empty.bugs).toEqual([]);
    expect(empty.participantsUserIds).toEqual([]);
    expect(empty.creatorTeamId).toBeNull();
  });
});

/*
 * Ниже — проверки уровня типов: их держит `tsc --noEmit` в гейте frontend.
 * Каждый `@ts-expect-error` краснеет, если удалённый ключ вернётся в форму списка.
 */

// @ts-expect-error `links` в элементе списка нет: LIST их не загружает
const readLinks = (report: ReportListItem) => report.links;

// @ts-expect-error `attachments` у бага в списке нет
const readBugAttachments = (bug: BugListItem) => bug.attachments;

// @ts-expect-error `steps` у бага в списке нет
const readBugSteps = (bug: BugListItem) => bug.steps;

// То, что список действительно отдаёт, читается без ошибок.
const readBugStatuses = (report: ReportListItem): BugStatuses[] =>
  report.bugs?.map((bug) => bug.status) ?? [];

const countComments = (bug: BugListItem): number => bug.comments?.length ?? 0;

describe("тип элемента списка", () => {
  it("запрещает удалённые ключи и разрешает оставшиеся", () => {
    const report = (
      convertObjectToCamel(wireListResponse) as ListReportsResponse
    ).reports[0];

    expect(readBugStatuses(report)).toEqual(["open"]);
    expect(countComments(report.bugs![0])).toBe(1);
    // Обращения выше существуют только ради @ts-expect-error — в рантайме undefined.
    expect(readLinks(report)).toBeUndefined();
    expect(readBugAttachments(report.bugs![0])).toBeUndefined();
    expect(readBugSteps(report.bugs![0])).toBeUndefined();
  });
});

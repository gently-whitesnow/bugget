import { describe, expect, it } from "vitest";
import { convertObjectToCamel } from "@/shared/lib/convertCases";
import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Проверки сужения контракта `reports` со стороны фронта — то, что не покрывает
 * `reports.test.ts`: полнота case-конверсии на всех уровнях вложенности.
 * Сверка рукописного DTO карточки с публичной формой вложения живёт в слое
 * `entities` (`entities/report/api/contracts.test.ts`) — `shared` не имеет права
 * туда смотреть.
 */

/**
 * Рекурсивно собирает все ключи объекта, включая элементы массивов.
 */
const collectKeys = (value: unknown, acc: string[] = []): string[] => {
  if (value === null || typeof value !== "object") return acc;

  if (Array.isArray(value)) {
    value.forEach((item) => collectKeys(item, acc));
    return acc;
  }

  for (const [key, nested] of Object.entries(
    value as Record<string, unknown>
  )) {
    acc.push(key);
    collectKeys(nested, acc);
  }

  return acc;
};

const wireListResponse: components["schemas"]["ReportList"] = {
  total: "1",
  reports: [
    {
      id: "42",
      title: "Не открывается карточка",
      status: 0,
      responsible_user_id: "u-1",
      past_responsible_user_id: "u-2",
      creator_user_id: "u-3",
      creator_team_id: "t-1",
      created_at: "2026-07-01T10:00:00Z",
      updated_at: "2026-07-02T10:00:00Z",
      creator_type: 0,
      is_excluded_from_analytics: false,
      participants_user_ids: ["u-1"],
      bugs: [
        {
          id: 1,
          report_id: 42,
          title: "падает",
          receive: "падает",
          expect: "не падает",
          created_at: "2026-07-01T10:00:00Z",
          updated_at: "2026-07-01T10:00:00Z",
          creator_user_id: "u-3",
          status: 0,
          creator_type: 0,
          comments: [
            {
              id: 7,
              bug_id: 1,
              text: "воспроизвёл",
              creator_user_id: "u-1",
              creator_type: 0,
              audience: 0,
              created_at: "2026-07-01T11:00:00Z",
              updated_at: "2026-07-01T11:00:00Z",
              attachments: null,
            },
          ],
        },
      ],
    },
  ],
};

describe("сужение контракта reports на фронте", () => {
  it("case-конверсия доходит до самого глубокого уровня — snake_case ключей не остаётся", () => {
    // Тип обещает camelCase на всех уровнях; если интерсептор где-то не спускается
    // вглубь (например, в элементы массива комментариев), обещание — ложь.
    const converted = convertObjectToCamel(wireListResponse);

    expect(collectKeys(converted).filter((key) => key.includes("_"))).toEqual(
      []
    );
  });

  it("глубоко вложенный комментарий приходит в camelCase", () => {
    const list = convertObjectToCamel(wireListResponse) as Camelized<
      components["schemas"]["ReportList"]
    >;
    const comment = list.reports[0].bugs?.[0].comments?.[0];

    expect(comment?.bugId).toBe(1);
    expect(comment?.creatorUserId).toBe("u-1");
    expect(comment?.creatorType).toBe(0);
    // Комментарии список грузит, а их вложения — нет: ключ остаётся, значение null.
    expect(comment?.attachments).toBeNull();
  });
});

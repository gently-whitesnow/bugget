import { describe, expect, it } from "vitest";
import { buildQueryString } from "./buildQuery";
import type { operations } from "@/shared/api/generated/reports";

/**
 * Провод query-параметров. Имена типизированы контрактом, а форма сериализации
 * обязана остаться той, что писал рукописный `URLSearchParams`: массив —
 * повторяющимся ключом, а не `key[]`.
 */

type ListReportsQuery = NonNullable<
  operations["Reports_ListReports"]["parameters"]["query"]
>;

describe("buildQueryString", () => {
  it("массив кладёт повторяющимся ключом, без скобок и без запятых", () => {
    const query: ListReportsQuery = {
      reportStatuses: ["backlog", "fix", "test"],
    };

    expect(buildQueryString(query)).toBe(
      "reportStatuses=backlog&reportStatuses=fix&reportStatuses=test"
    );
  });

  it("пропускает null и undefined — параметра в URL нет вовсе", () => {
    const query: ListReportsQuery = {
      userId: undefined,
      teamId: null,
      reportStatuses: null,
      skip: 0,
      take: 10,
    };

    expect(buildQueryString(query)).toBe("skip=0&take=10");
  });

  it("сохраняет порядок и camelCase имён из контракта", () => {
    const query: ListReportsQuery = {
      userId: "u-1",
      teamId: "t-1",
      reportStatuses: ["resolved"],
      creatorTypes: ["user"],
      skip: 20,
      take: 10,
    };

    expect(buildQueryString(query)).toBe(
      "userId=u-1&teamId=t-1&reportStatuses=resolved&creatorTypes=user&skip=20&take=10"
    );
  });

  it("не путает 0 и false с отсутствием значения", () => {
    expect(buildQueryString({ skip: 0, flag: false })).toBe(
      "skip=0&flag=false"
    );
  });

  it("экранирует значения", () => {
    expect(buildQueryString({ query: "падает карточка & всё" })).toBe(
      "query=%D0%BF%D0%B0%D0%B4%D0%B0%D0%B5%D1%82+%D0%BA%D0%B0%D1%80%D1%82%D0%BE%D1%87%D0%BA%D0%B0+%26+%D0%B2%D1%81%D1%91"
    );
  });
});

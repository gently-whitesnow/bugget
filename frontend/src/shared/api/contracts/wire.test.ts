import { describe, expect, it } from "vitest";
import { convertObjectToCamel, snakeToCamel } from "@/shared/lib/convertCases";
import type { SnakeToCamel, Wire } from "./wire";

/**
 * `Wire<T>` работает только пока он совпадает с рантаймовой перекладкой ключей
 * в `shared/api/instances/base.ts`. Совпадение проверяется с двух сторон:
 * компилятором (`Assert<Equals<...>>` — расхождение валит frontend-typecheck)
 * и рантаймом (`snakeToCamel` на тех же ключах).
 */

type Equals<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;
type Assert<T extends true> = T;

// --- ключи -------------------------------------------------------------------

type _KeySingleWord = Assert<Equals<SnakeToCamel<"id">, "id">>;
type _KeyTwoWords = Assert<Equals<SnakeToCamel<"created_at">, "createdAt">>;
type _KeyThreeWords = Assert<
  Equals<SnakeToCamel<"is_gzip_compressed">, "isGzipCompressed">
>;
type _KeyDigits = Assert<Equals<SnakeToCamel<"legacy_id_2">, "legacyId2">>;

// --- структуры ---------------------------------------------------------------

type _Primitive = Assert<Equals<Wire<string>, string>>;
type _Nullable = Assert<
  Equals<Wire<{ a_b: string } | null>, { aB: string } | null>
>;

type _Optional = Assert<
  Equals<Wire<{ a_b?: string; c_d: number }>, { aB?: string; cD: number }>
>;

type _Nested = Assert<
  Equals<
    Wire<{ outer_field: { inner_field: string }[] }>,
    { outerField: { innerField: string }[] }
  >
>;

type _NullableArray = Assert<
  Equals<
    Wire<{ items_list: { item_id: number }[] | null }>,
    { itemsList: { itemId: number }[] | null }
  >
>;

// Ссылки на type-level проверки, чтобы noUnusedLocals не считал их мусором.
export type WireTypeAssertions = [
  _KeySingleWord,
  _KeyTwoWords,
  _KeyThreeWords,
  _KeyDigits,
  _Primitive,
  _Nullable,
  _Optional,
  _Nested,
  _NullableArray,
];

describe("SnakeToCamel совпадает с рантаймовым snakeToCamel", () => {
  // Ключи взяты из реальных схем specs/contracts: Attachment, BugStep, Report.
  const contractKeys = [
    "id",
    "entity_id",
    "attach_type",
    "storage_key",
    "storage_kind",
    "created_at",
    "creator_user_id",
    "length_bytes",
    "file_name",
    "mime_type",
    "has_preview",
    "is_gzip_compressed",
    "is_excluded_from_analytics",
    "past_responsible_user_id",
    "participants_user_ids",
    "step_number",
  ] as const;

  const expected: { [K in (typeof contractKeys)[number]]: SnakeToCamel<K> } = {
    id: "id",
    entity_id: "entityId",
    attach_type: "attachType",
    storage_key: "storageKey",
    storage_kind: "storageKind",
    created_at: "createdAt",
    creator_user_id: "creatorUserId",
    length_bytes: "lengthBytes",
    file_name: "fileName",
    mime_type: "mimeType",
    has_preview: "hasPreview",
    is_gzip_compressed: "isGzipCompressed",
    is_excluded_from_analytics: "isExcludedFromAnalytics",
    past_responsible_user_id: "pastResponsibleUserId",
    participants_user_ids: "participantsUserIds",
    step_number: "stepNumber",
  };

  it.each(contractKeys)("%s", (key) => {
    expect(snakeToCamel(key)).toBe(expected[key]);
  });
});

describe("Wire повторяет convertObjectToCamel на вложенных структурах", () => {
  it("переводит ключи вложенных объектов и массивов", () => {
    const wirePayload = {
      report_id: "abc",
      is_excluded_from_analytics: true,
      bugs: [
        {
          bug_id: 1,
          steps: [{ step_number: 1, creator_user_id: "u1" }],
        },
      ],
    };

    const converted = convertObjectToCamel(wirePayload) as Wire<
      typeof wirePayload
    >;

    expect(converted).toEqual({
      reportId: "abc",
      isExcludedFromAnalytics: true,
      bugs: [
        {
          bugId: 1,
          steps: [{ stepNumber: 1, creatorUserId: "u1" }],
        },
      ],
    });

    // Проверка типом: доступ по camelCase-ключу компилируется, а обращение
    // к исходному snake_case-ключу — нет.
    expect(converted.bugs[0].steps[0].stepNumber).toBe(1);
  });
});

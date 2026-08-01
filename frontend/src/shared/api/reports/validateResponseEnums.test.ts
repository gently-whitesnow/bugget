// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { validateReportsResponseEnums } from "./validateResponseEnums";

describe("закрытые enum-поля reports HTTP response", () => {
  it.each([0, "unknown"])(
    "отклоняет значение %p во всех целевых полях",
    (invalid) => {
      const samples = [
        ["status", { id: "report-1", status: invalid }],
        ["creatorType", { id: "report-1", creatorType: invalid }],
        ["status", { id: 1, status: invalid }],
        ["creatorType", { id: 1, creatorType: invalid }],
        ["creatorType", { id: 2, creatorType: invalid, audience: "internal" }],
        ["audience", { id: 2, creatorType: "user", audience: invalid }],
        ["attachType", { id: 3, attachType: invalid }],
      ] as const;

      for (const [field, sample] of samples) {
        expect(() => validateReportsResponseEnums(sample)).toThrow(field);
      }
    }
  );

  it("принимает все известные значения во вложенном ответе", () => {
    const response = {
      reports: [
        {
          id: "report-1",
          status: "test",
          creatorType: "tg_beta_tester",
          bugs: [
            {
              id: 1,
              status: "fixed",
              creatorType: "system",
              comments: [{ id: 2, creatorType: "user", audience: "external" }],
              attachments: [{ id: 3, attachType: "bug_step" }],
            },
          ],
        },
      ],
    };

    expect(() => validateReportsResponseEnums(response)).not.toThrow();
  });
});

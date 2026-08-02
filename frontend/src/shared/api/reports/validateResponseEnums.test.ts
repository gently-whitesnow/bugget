// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import type { HttpMethod } from "@/shared/api/operation";
import { validateReportsResponseEnums } from "./validateResponseEnums";

const validate = (path: string, method: HttpMethod, value: unknown) =>
  validateReportsResponseEnums(value, { path, method });

describe("закрытые enum-поля reports HTTP response", () => {
  it.each([0, "unknown", null, "FIXED"])(
    "отклоняет значение %p во всех целевых полях",
    (invalid) => {
      const samples = [
        ["/v2/reports", "post", "status", { status: invalid }],
        ["/v2/reports", "post", "creatorType", { creatorType: invalid }],
        ["/v2/reports/{aliasId}/bugs", "post", "status", { status: invalid }],
        [
          "/v2/reports/{aliasId}/bugs",
          "post",
          "creatorType",
          { creatorType: invalid },
        ],
        [
          "/v2/reports/{aliasId}/bugs/{bugId}/comments",
          "post",
          "creatorType",
          { creatorType: invalid, audience: "internal" },
        ],
        [
          "/v2/reports/{aliasId}/bugs/{bugId}/comments",
          "post",
          "audience",
          { creatorType: "user", audience: invalid },
        ],
        [
          "/v2/reports/{aliasId}/bugs/{bugId}/attachments",
          "post",
          "attachType",
          { attachType: invalid },
        ],
      ] as const;

      for (const [path, method, field, sample] of samples) {
        expect(() => validate(path, method, sample)).toThrow(field);
      }
    }
  );

  it("валидирует вложенные поля по известной схеме операции", () => {
    const response = {
      reports: [
        {
          status: "test",
          creatorType: "tg_beta_tester",
          bugs: [
            {
              status: "fixed",
              creatorType: "system",
              comments: [{ creatorType: "user", audience: "external" }],
              attachments: [{ attachType: "bug_step" }],
            },
          ],
        },
      ],
    };

    expect(() => validate("/v2/reports", "get", response)).not.toThrow();
  });

  it("не классифицирует произвольный объект по id/status", () => {
    expect(() =>
      validate("/v2/reports/{id}/analytics", "get", {
        id: "looks-like-report",
        status: 0,
      })
    ).not.toThrow();
  });

  it("не смешивает ReportStatus и BugStatus", () => {
    expect(() => validate("/v2/reports", "post", { status: "open" })).toThrow(
      "status"
    );
    expect(() =>
      validate("/v2/reports/{aliasId}/bugs", "post", { status: "backlog" })
    ).toThrow("status");
  });
});

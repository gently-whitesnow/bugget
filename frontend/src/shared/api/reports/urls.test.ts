// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { setAppContext } from "@/shared/api/instances";
import { attachmentContentPath, attachmentContentUrl } from "./urls";

/**
 * Адрес содержимого вложения уезжает в `src` картинки и в `fetch` за телом, а не
 * в axios. До миграции он собирался строкой в `shared/ui/FilePreview`; теперь
 * шаблон берётся из контракта, и тест фиксирует, что получившийся адрес совпал
 * со старым дословно — включая хвостовой слэш у оригинала.
 */

beforeEach(() => {
  setAppContext(1, 2);
});

afterEach(() => {
  setAppContext(null, null);
});

describe("адрес содержимого вложения", () => {
  it("вложение бага: оригинал с хвостовым слэшем, превью без него", () => {
    expect(attachmentContentPath({ reportId: "team-1", bugId: 2, id: 3 })).toBe(
      "/v2/reports/team-1/bugs/2/attachments/3/content/"
    );

    expect(
      attachmentContentPath({
        reportId: "team-1",
        bugId: 2,
        id: 3,
        preview: true,
      })
    ).toBe("/v2/reports/team-1/bugs/2/attachments/3/content/preview");
  });

  it("вложение комментария и шага сохраняют свои сегменты", () => {
    expect(
      attachmentContentPath({
        reportId: "team-1",
        bugId: 2,
        id: 3,
        commentId: 4,
      })
    ).toBe("/v2/reports/team-1/bugs/2/comments/4/attachments/3/content/");

    expect(
      attachmentContentPath({
        reportId: "team-1",
        bugId: 2,
        id: 3,
        stepId: 5,
        preview: true,
      })
    ).toBe("/v2/reports/team-1/bugs/2/steps/5/attachments/3/content/preview");
  });

  it("комментарий выигрывает у шага — тот же приоритет, что и в старом коде", () => {
    expect(
      attachmentContentPath({
        reportId: "team-1",
        bugId: 2,
        id: 3,
        commentId: 4,
        stepId: 5,
      })
    ).toBe("/v2/reports/team-1/bugs/2/comments/4/attachments/3/content/");
  });

  it("полный адрес получает префикс контекста и origin страницы", () => {
    expect(attachmentContentUrl({ reportId: "team-1", bugId: 2, id: 3 })).toBe(
      `${window.location.origin}/api/app/workspaces/1/teams/2/v2/reports/team-1/bugs/2/attachments/3/content/`
    );
  });

  it("без контекста остаётся прежний fallback без workspace и team", () => {
    setAppContext(null, null);

    expect(attachmentContentUrl({ reportId: "team-1", bugId: 2, id: 3 })).toBe(
      `${window.location.origin}/api/app/v2/reports/team-1/bugs/2/attachments/3/content/`
    );
  });
});

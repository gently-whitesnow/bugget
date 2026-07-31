// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { setAppContext } from "@/shared/api/instances";
import type { paths } from "@/shared/api/generated/reports";
import {
  ATTACHMENT_CONTENT,
  attachmentContentPath,
  attachmentContentUrl,
} from "./urls";
import type { AttachmentContentRoute } from "./urls";

/**
 * Адрес содержимого вложения уезжает в `src` картинки и в `fetch` за телом, а не
 * в axios. До миграции он собирался строкой в `shared/ui/FilePreview`.
 *
 * Проверяется две вещи. Первая — compile-time: все шесть шаблонов (оригинал и
 * превью каждого владельца) существуют в `paths` сгенерированного контракта,
 * причём превью берётся своим путём, а не дописанным суффиксом. Вторая — рантайм:
 * получившийся адрес совпал со старым дословно, включая хвостовой слэш у
 * оригинала.
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

/*
 * Проверки уровня типов: их держит `tsc --noEmit` в гейте frontend-typecheck.
 * Переименовали любой из шести путей в `specs/contracts/reports/openapi.yaml` —
 * красной становится сборка, а не только этот тест.
 */

/** Каждый шаблон обязан быть ключом `paths`, а не произвольной строкой. */
const routesAreContractPaths: readonly (keyof paths)[] = [
  ATTACHMENT_CONTENT.bug.original,
  ATTACHMENT_CONTENT.bug.preview,
  ATTACHMENT_CONTENT.comment.original,
  ATTACHMENT_CONTENT.comment.preview,
  ATTACHMENT_CONTENT.step.original,
  ATTACHMENT_CONTENT.step.preview,
];

/**
 * Превью — самостоятельный путь контракта: тип шаблона превью совпадает с ключом
 * `paths`, а не выводится из пути оригинала конкатенацией. Строка, собранная
 * склейкой, ключом `paths` не является, и присвоение ниже её бы не приняло.
 */
const previewIsOwnContractPath: keyof paths = ATTACHMENT_CONTENT.bug.preview;

/** Публичный тип маршрута тоже сужен до ключей контракта. */
const routeTypeIsContractBound: keyof paths = ATTACHMENT_CONTENT.comment
  .preview satisfies AttachmentContentRoute;

describe("шаблоны адресов связаны с контрактом на этапе компиляции", () => {
  it("все шесть путей объявлены в generated paths", () => {
    // Равенства держит `tsc --noEmit`; тест фиксирует намерение и состав набора.
    expect(routesAreContractPaths).toHaveLength(6);
    expect(new Set(routesAreContractPaths).size).toBe(6);
    expect(previewIsOwnContractPath).toBe(
      "/v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}/content/preview"
    );
    expect(routeTypeIsContractBound).toContain("/content/preview");
  });

  it("построенный адрес — это подставленный шаблон контракта, а не склейка", () => {
    const preview = attachmentContentPath({
      reportId: "team-1",
      bugId: 2,
      id: 3,
      preview: true,
    });

    expect(preview).toBe(
      ATTACHMENT_CONTENT.bug.preview
        .replace("{aliasId}", "team-1")
        .replace("{bugId}", "2")
        .replace("{id}", "3")
    );
  });
});

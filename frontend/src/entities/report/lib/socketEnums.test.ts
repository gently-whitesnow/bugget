// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import {
  AttachmentTypes,
  BugStatuses,
  CommentAudiences,
  CreatorTypes,
  ReportStatuses,
} from "@/shared/config";
import {
  attachTypeFromSocket,
  bugStatusFromSocket,
  commentAudienceFromSocket,
  creatorTypeFromSocket,
  reportStatusFromSocket,
} from "./socketEnums";
import { resolveCreatorName } from "./creatorResolvers";

/**
 * Шов «числа realtime → значения провода».
 *
 * HTTP перешёл на строки (ADR-0013), SignalR остался числовым (ADR-0007), и
 * ошибка на этом шве не видна типами: и там и там сегодня «одно поле статуса».
 * Поэтому проверяется весь диапазон каждого enum'а, а не happy path.
 */
describe("числа SignalR переводятся в значения провода", () => {
  it.each([
    [0, ReportStatuses.BACKLOG],
    [1, ReportStatuses.RESOLVED],
    [2, ReportStatuses.FIX],
    [3, ReportStatuses.REJECTED],
    [4, ReportStatuses.TEST],
  ])("status репорта %i → %s", (value, expected) => {
    expect(reportStatusFromSocket(value)).toBe(expected);
  });

  it.each([
    [0, BugStatuses.OPEN],
    [1, BugStatuses.VERIFIED],
    [2, BugStatuses.REJECTED],
    [3, BugStatuses.FIXED],
  ])("status бага %i → %s", (value, expected) => {
    expect(bugStatusFromSocket(value)).toBe(expected);
  });

  it.each([
    [0, CreatorTypes.USER],
    [1, CreatorTypes.SYSTEM],
    [2, CreatorTypes.TG_BETA_TESTER],
    [3, CreatorTypes.AGENT],
  ])("creatorType %i → %s", (value, expected) => {
    expect(creatorTypeFromSocket(value)).toBe(expected);
  });

  it.each([
    [0, CommentAudiences.INTERNAL],
    [1, CommentAudiences.EXTERNAL],
  ])("audience %i → %s", (value, expected) => {
    expect(commentAudienceFromSocket(value)).toBe(expected);
  });

  it.each([
    [0, AttachmentTypes.FACT],
    [1, AttachmentTypes.EXPECT],
    [2, AttachmentTypes.COMMENT],
    [3, AttachmentTypes.BUG_STEP],
  ])("attachType %i → %s", (value, expected) => {
    expect(attachTypeFromSocket(value)).toBe(expected);
  });
});

/**
 * Неизвестное число — расхождение realtime-контракта с фронтом. Подстановка
 * «нулевого» значения замаскировала бы его чужим статусом в сторе, поэтому шов
 * падает громко.
 */
describe("неизвестное число не подменяется ближайшим", () => {
  it.each([-1, 5, 99])("status репорта %i отвергается", (value) => {
    expect(() => reportStatusFromSocket(value)).toThrow(/неизвестное значение/);
  });

  it("creatorType вне диапазона не становится user", () => {
    expect(() => creatorTypeFromSocket(7)).toThrow(/неизвестное значение/);
  });
});

/**
 * `tg_beta_tester` был на проводе и раньше, но в константах фронта его не было,
 * и внешний автор разрешался как внутренний пользователь.
 */
describe("внешний автор через beta-test bot", () => {
  it("не выдаётся за системного", () => {
    const users = {
      "u-1": { name: "Тестер" },
    } as unknown as Parameters<typeof resolveCreatorName>[2]["users"];

    const name = resolveCreatorName("u-1", CreatorTypes.TG_BETA_TESTER, {
      users,
    });

    expect(name).toBe("Тестер");
  });

  it("неизвестный HTTP creator_type не маскируется под внутреннего пользователя", () => {
    const users = {
      "u-1": { name: "Внутренний пользователь" },
    } as unknown as Parameters<typeof resolveCreatorName>[2]["users"];

    const name = resolveCreatorName("u-1", "unknown" as never, { users });

    expect(name).not.toBe("Внутренний пользователь");
  });
});

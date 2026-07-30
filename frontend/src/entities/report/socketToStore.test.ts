// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { CreatorTypes } from "@/shared/config";
import type { CreateBugSocketResponse } from "@/shared/model";
import { $bugsStore, clearBugsEvent, createBugSocketEvent } from "./model";
import {
  bugFromSocket,
  commentFromSocket,
  commentUpdateFromSocket,
} from "./lib/fromSocket";

/**
 * Шов realtime → стор.
 *
 * `ReceiveBugCreate` публикует `BugSummaryDbModel`, где `CreatorType` обязателен.
 * Раньше зеркало SignalR это поле теряло, а стор подставлял константу — тест
 * падает, если поле снова пропадёт из payload'а или в сторе опять появится
 * значение по умолчанию: `SYSTEM` не совпадёт с `USER`.
 */

/** Строгое равенство типов: при расхождении `false` не присвоится `true`. */
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;

// Поле обязано остаться в payload'е realtime-контракта: без него не соберётся тип.
const payloadCarriesCreatorType: Equal<
  CreateBugSocketResponse["creatorType"],
  number
> = true;

const socketBug = (creatorType: number): CreateBugSocketResponse => ({
  id: 7,
  title: "падает карточка",
  receive: "падает",
  expect: null,
  createdAt: "2026-07-30T10:00:00Z",
  updatedAt: "2026-07-30T10:00:00Z",
  creatorUserId: "u-1",
  creatorType,
  status: 0,
});

afterEach(() => {
  clearBugsEvent();
});

describe("баг из события SignalR попадает в стор", () => {
  it("тип автора берётся с провода, а не из константы", () => {
    expect(payloadCarriesCreatorType).toBe(true);

    createBugSocketEvent({
      reportId: "team-42",
      bug: socketBug(CreatorTypes.SYSTEM),
    });

    expect($bugsStore.getState()[7].creatorType).toBe(CreatorTypes.SYSTEM);
    expect($bugsStore.getState()[7].creatorType).not.toBe(CreatorTypes.USER);
  });

  it("человеческий автор доезжает так же", () => {
    createBugSocketEvent({
      reportId: "team-42",
      bug: socketBug(CreatorTypes.USER),
    });

    expect($bugsStore.getState()[7].creatorType).toBe(CreatorTypes.USER);
  });

  it("reportId в сторе — alias открытого репорта, а не поле payload'а", () => {
    createBugSocketEvent({
      reportId: "team-42",
      bug: socketBug(CreatorTypes.USER),
    });

    expect($bugsStore.getState()[7].reportId).toBe("team-42");
  });
});

describe("адаптеры payload → сущность стора", () => {
  it("баг переносится целиком: ни одно поле не теряется по дороге", () => {
    expect(bugFromSocket(socketBug(CreatorTypes.SYSTEM), "team-42")).toEqual({
      id: 7,
      reportId: "team-42",
      title: "падает карточка",
      receive: "падает",
      expect: null,
      creatorUserId: "u-1",
      creatorType: CreatorTypes.SYSTEM,
      createdAt: "2026-07-30T10:00:00Z",
      updatedAt: "2026-07-30T10:00:00Z",
      status: 0,
      // Вложения, комментарии и шаги это событие не приносит.
      attachments: null,
      comments: null,
      clientId: 7,
      isLocalOnly: false,
    });
  });

  it("комментарий из события кладётся с attachments: null", () => {
    const comment = commentFromSocket({
      id: 3,
      bugId: 7,
      text: "воспроизвёл",
      creatorType: CreatorTypes.USER,
      audience: 0,
      creatorUserId: "u-2",
      createdAt: "2026-07-30T10:01:00Z",
      updatedAt: "2026-07-30T10:01:00Z",
    });

    expect(comment.attachments).toBeNull();
    expect(comment.creatorType).toBe(CreatorTypes.USER);
  });

  it("обновление комментария не теряет уже загруженные вложения", () => {
    const existing = {
      ...commentFromSocket({
        id: 3,
        bugId: 7,
        text: "воспроизвёл",
        creatorType: CreatorTypes.USER,
        audience: 0,
        creatorUserId: "u-2",
        createdAt: "2026-07-30T10:01:00Z",
        updatedAt: "2026-07-30T10:01:00Z",
      }),
      attachments: [
        {
          id: 11,
          entityId: 3,
          attachType: 2,
          createdAt: "2026-07-30T10:02:00Z",
          creatorUserId: "u-2",
          fileName: "скрин.png",
          hasPreview: true,
        },
      ],
    };

    const updated = commentUpdateFromSocket(existing, {
      id: 3,
      bugId: 7,
      text: "перепроверил",
      creatorType: CreatorTypes.USER,
      audience: 0,
      creatorUserId: "u-2",
      createdAt: "2026-07-30T10:01:00Z",
      updatedAt: "2026-07-30T10:03:00Z",
    });

    expect(updated.text).toBe("перепроверил");
    expect(updated.attachments).toEqual(existing.attachments);
  });
});

import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { appApi, setAppContext } from "@/shared/api";
import type { components } from "@/shared/api/generated/external";
import {
  applyExternalSearchResult,
  batchGetKaitenBoards,
  searchExternal,
  searchKaitenBoards,
} from "./index";

/**
 * Провод модуля `external` после перевода на сгенерированный контракт.
 *
 * Типы теперь выведены из yaml, но URL, порядок и имена query, форма тела
 * обязаны остаться прежними 1:1 — фронт стоит в проде у заказчика. Поэтому тест
 * смотрит не на типы (их держит `tsc`), а на то, что реально уходит в сеть.
 */

const contextPrefix = "/api/app/workspaces/1/teams/2";

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;

const sent = () => {
  if (!captured) throw new Error("Запрос не был отправлен");
  return captured;
};

const wireSearch: components["schemas"]["ExternalSearchResult"] = {
  total: "1",
  items: [{ id: "42", text: "Карточка", source: "kaiten" }],
};

const wireBoards: components["schemas"]["KaitenBoard"][] = [
  { id: 7, title: "Доска", space_title: "Пространство", space_id: 3 },
];

const respondWith = (data: unknown) => {
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
};

beforeEach(() => {
  setAppContext(1, 2);
  captured = null;
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  respondWith(null);
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  setAppContext(null, null);
});

describe("поиск по внешним источникам", () => {
  it("сохраняет адрес и порядок query", async () => {
    respondWith(wireSearch);

    const page = await searchExternal({ query: "карточка", skip: 0, take: 7 });

    expect(sent().method).toBe("get");
    expect(sent().url).toBe(
      `${contextPrefix}/v1/external/search?query=%D0%BA%D0%B0%D1%80%D1%82%D0%BE%D1%87%D0%BA%D0%B0&skip=0&take=7`
    );
    expect(page.items[0].source).toBe("kaiten");
  });

  it("пустая строка поиска уходит на провод, а не пропадает", async () => {
    respondWith(wireSearch);

    await searchExternal({ query: "", skip: 0, take: 7 });

    expect(sent().url).toBe(
      `${contextPrefix}/v1/external/search?query=&skip=0&take=7`
    );
  });

  it("привязка результата: POST с телом в snake_case контракта", async () => {
    await applyExternalSearchResult({
      id: "42",
      source: "kaiten",
      reportId: "team-1",
    });

    expect(sent().method).toBe("post");
    expect(sent().url).toBe(`${contextPrefix}/v1/external/search/apply`);
    expect(JSON.parse(sent().data as string)).toEqual({
      id: "42",
      source: "kaiten",
      report_id: "team-1",
    });
  });
});

describe("доски Kaiten", () => {
  it("без фильтра адрес остаётся без хвостового «?»", async () => {
    respondWith(wireBoards);

    const boards = await searchKaitenBoards();

    expect(sent().method).toBe("get");
    expect(sent().url).toBe(`${contextPrefix}/v1/external/kaiten/boards`);
    expect(boards[0].spaceTitle).toBe("Пространство");
    expect(boards[0]).not.toHaveProperty("space_title");
  });

  it("с фильтром добавляет query по имени из контракта", async () => {
    respondWith(wireBoards);

    await searchKaitenBoards("дос");

    expect(sent().url).toBe(
      `${contextPrefix}/v1/external/kaiten/boards?query=%D0%B4%D0%BE%D1%81`
    );
  });

  it("batch-get отправляет идентификаторы телом", async () => {
    respondWith(wireBoards);

    await batchGetKaitenBoards({ ids: [7, 8] });

    expect(sent().method).toBe("post");
    expect(sent().url).toBe(
      `${contextPrefix}/v1/external/kaiten/boards/batch-get`
    );
    expect(JSON.parse(sent().data as string)).toEqual({ ids: [7, 8] });
  });
});

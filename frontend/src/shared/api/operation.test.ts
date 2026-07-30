// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { createApiInstance } from "./instances/base";
import { createOperationRequest } from "./operation";
import type { OperationCallArgs } from "./operation";
import type { paths } from "@/shared/api/generated/reports";

/**
 * Граница «операция контракта → HTTP».
 *
 * Две вещи, которые она обязана сохранять и которые не видны из типов вызова:
 * обязательность query там, где её требует контракт, и адрес на проводе — включая
 * разницу между «query не передан» и «query передан, но пуст».
 */

/** Строгое равенство типов: при расхождении `false` не присвоится `true`. */
type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2
    ? true
    : false;

type UploadAttachment = "/v2/reports/{aliasId}/bugs/{bugId}/attachments";

type UploadArgs = OperationCallArgs<paths, UploadAttachment, "post">;
type ListArgs = OperationCallArgs<paths, "/v2/reports", "get">;

/**
 * Контракт объявляет `attachType` обязательным query-параметром загрузки
 * вложения. Аргументы без `query` не должны подходить под тип вызова: если
 * обязательность снова потеряется, `extends` станет истинным и `false` не
 * присвоится в `true` — гейт `frontend-typecheck` покраснеет.
 */
type UploadArgsWithoutQuery = {
  path: { aliasId: string; bugId: number };
  multipart: { file: File };
};

const requiredQueryCannotBeOmitted: UploadArgsWithoutQuery extends UploadArgs
  ? false
  : true = true;

/** А сам query у этой операции обязателен и по типу свойства. */
const uploadQueryIsRequired: Equal<
  undefined extends UploadArgs["query"] ? true : false,
  false
> = true;

/** У списка фильтры необязательны — вызов без query остаётся законным. */
const optionalQueryStaysOptional: object extends ListArgs ? true : false = true;

const instanceCapturingRequest = () => {
  let captured: InternalAxiosRequestConfig | null = null;
  const instance = createApiInstance();
  instance.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: null,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;

  return {
    request: createOperationRequest<paths>(instance),
    sent: () => {
      if (!captured) throw new Error("Запрос не был отправлен");
      return captured;
    },
  };
};

describe("обязательность query выведена из контракта", () => {
  it("query загрузки вложения обязателен, у списка — нет", () => {
    expect(requiredQueryCannotBeOmitted).toBe(true);
    expect(uploadQueryIsRequired).toBe(true);
    expect(optionalQueryStaysOptional).toBe(true);
  });
});

describe("адрес на проводе", () => {
  it("переданный, но пустой query оставляет хвостовой «?» — как рукописный вызов", async () => {
    const { request, sent } = instanceCapturingRequest();

    await request("/v1/reports/search", "get", { query: {} });

    expect(sent().url).toBe("/v1/reports/search?");
  });

  it("пустой query из одних undefined ведёт себя так же", async () => {
    const { request, sent } = instanceCapturingRequest();

    await request("/v1/reports/search", "get", {
      query: { query: undefined, skip: undefined },
    });

    expect(sent().url).toBe("/v1/reports/search?");
  });

  it("операция без query уходит без «?» вовсе", async () => {
    const { request, sent } = instanceCapturingRequest();

    await request("/v2/reports/{aliasId}", "get", {
      path: { aliasId: "team-42" },
    });

    expect(sent().url).toBe("/v2/reports/team-42");
  });

  it("path-параметры подставляются дословно, без экранирования", async () => {
    const { request, sent } = instanceCapturingRequest();

    await request(
      "/v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}",
      "patch",
      {
        path: { aliasId: "team-42", bugId: 7, stepId: 3 },
        body: { text: "шаг" },
      }
    );

    expect(sent().url).toBe("/v2/reports/team-42/bugs/7/steps/3");
  });

  it("непереданный path-параметр — ошибка, а не «undefined» в адресе", async () => {
    const { request } = instanceCapturingRequest();

    await expect(
      request("/v2/reports/{aliasId}", "get", {
        path: { aliasId: undefined as unknown as string },
      })
    ).rejects.toThrow("Не задан path-параметр aliasId");
  });
});

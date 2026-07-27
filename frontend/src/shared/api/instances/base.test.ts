// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import type { AxiosAdapter } from "axios";
import { createApiInstance } from "./base";

/**
 * Подменяем транспорт: интерцепторы отрабатывают ровно так же, как на живом ответе,
 * но без сети.
 */
const instanceRespondingWith = (data: unknown, contentType: string) => {
  const instance = createApiInstance();
  const adapter: AxiosAdapter = async (config) => ({
    data,
    status: 200,
    statusText: "OK",
    headers: { "content-type": contentType },
    config,
  });
  instance.defaults.adapter = adapter;
  return instance;
};

describe("response-интерцептор: case-конверсия", () => {
  it("не конвертирует ключи application/problem+json — словарь errors остаётся как есть", async () => {
    const instance = instanceRespondingWith(
      {
        type: "urn:bugget:error:model_state_validation_error",
        status: 400,
        code: "model_state_validation_error",
        errors: { report_title: ["Поле обязательно"], expected_result: ["!"] },
      },
      "application/problem+json; charset=utf-8"
    );

    const response = await instance.get("/v2/reports");

    expect(response.data.errors).toEqual({
      report_title: ["Поле обязательно"],
      expected_result: ["!"],
    });
    expect(response.data.code).toBe("model_state_validation_error");
  });

  it("обычный application/json конвертирует в camelCase, как и раньше", async () => {
    const instance = instanceRespondingWith(
      { report_title: "Падает карточка" },
      "application/json"
    );

    const response = await instance.get("/v2/reports");

    expect(response.data).toEqual({ reportTitle: "Падает карточка" });
  });

  it("не падает на ответе без Content-Type", async () => {
    const instance = createApiInstance();
    instance.defaults.adapter = async (config) => ({
      data: { report_title: "Падает карточка" },
      status: 200,
      statusText: "OK",
      headers: {},
      config,
    });

    const response = await instance.get("/v2/reports");

    expect(response.data).toEqual({ reportTitle: "Падает карточка" });
  });
});

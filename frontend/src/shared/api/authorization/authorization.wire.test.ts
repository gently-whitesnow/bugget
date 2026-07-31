// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { authorizationApi } from "@/shared/api/instances";
import type { components } from "@/shared/api/generated/authorization";
import { logout } from "./session";

/**
 * Провод модуля `authorization`.
 *
 * Ручка одна, но адрес у неё собирался руками (`authorizationPath("/logout")`),
 * а теперь берётся из контракта, а префикс модуля дописывает интерсептор
 * инстанса. На проводе обязан остаться тот же URL и тот же метод.
 */

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;

const wireLogout: components["schemas"]["LogoutResult"] = {
  redirect_url: "/login",
};

const sent = () => {
  if (!captured) throw new Error("Запрос не был отправлен");
  return captured;
};

beforeEach(() => {
  captured = null;
  originalAdapter = authorizationApi.defaults.adapter as
    | AxiosAdapter
    | undefined;
  authorizationApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: wireLogout,
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
});

afterEach(() => {
  authorizationApi.defaults.adapter = originalAdapter;
});

describe("выход из системы", () => {
  it("POST по прежнему публичному адресу, без тела и без query", async () => {
    await logout();

    expect(sent().method).toBe("post");
    expect(sent().url).toBe("/api/authorization/v1/logout");
    expect(sent().data).toBeUndefined();
  });

  it("ответ доезжает в camelCase, но переход по нему фронт не делает", async () => {
    const result = await logout();

    // `redirectUrl` контракт отдаёт, а уводит пользователя по-прежнему
    // `getPostLogoutRedirectUrl` — поведение слайс не менял.
    expect(result.redirectUrl).toBe("/login");
    expect(result).not.toHaveProperty("redirect_url");
  });
});

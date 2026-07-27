// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from "vitest";
import { AxiosError, type AxiosAdapter, type AxiosInstance } from "axios";
import { createApiInstance } from "./base";
import { parseApiError } from "../parseApiError";

/**
 * Adversarial-проверка MAIN-66: разбор ошибок проверяется на настоящем пути ошибки
 * axios (reject через интерцепторы), а не на выдуманном успешном ответе.
 *
 * Существующий `base.test.ts` подаёт `application/problem+json` со статусом 200 —
 * такого ответа на проводе не бывает: Problem Details приходит только с 4xx/5xx,
 * то есть по rejected-ветке. Здесь всё гоняется именно по ней.
 */

/** Транспорт, который отвечает ошибкой ровно так же, как настоящий adapter axios. */
const respondWithError = (
  status: number,
  data: unknown,
  headers: Record<string, string> = {}
): AxiosInstance => {
  const instance = createApiInstance();
  const adapter: AxiosAdapter = async (config) => {
    const response = { data, status, statusText: "", headers, config };
    throw new AxiosError(
      "Request failed",
      AxiosError.ERR_BAD_REQUEST,
      config,
      {} as never,
      response as never
    );
  };
  instance.defaults.adapter = adapter;
  return instance;
};

const failedRequest = async (instance: AxiosInstance): Promise<unknown> => {
  try {
    await instance.get("/api/users/v1/workspaces/1/teams");
    throw new Error("запрос обязан был упасть");
  } catch (error) {
    return error;
  }
};

/** ValidationProblemDetails: ключи `errors` — имена полей, их переименование ломает форму. */
const validationProblem = {
  type: "urn:bugget:error:model_state_validation_error",
  title: "Ошибка валидации",
  status: 400,
  code: "model_state_validation_error",
  errors: {
    report_id: ["Поле обязательно"],
    "$.expected_result": ["Некорректный JSON"],
    "Steps[0].ActualResult": ["Слишком длинно"],
    "attachments[0].file_name": ["Недопустимое имя"],
  },
};

beforeEach(() => {
  // 401-ветка интерцептора уводит браузер на /login; чтобы дойти до тела ответа,
  // тест уже «находится» на /login — тогда интерцептор просто пробрасывает ошибку.
  window.history.pushState({}, "", "/login");
});

describe("путь ошибки: ключи словаря errors", () => {
  it("problem+json с charset: имена полей формы доезжают без переименования", async () => {
    const error = await failedRequest(
      respondWithError(400, validationProblem, {
        "content-type": "application/problem+json; charset=utf-8",
      })
    );

    const body = (error as AxiosError).response
      ?.data as typeof validationProblem;
    expect(Object.keys(body.errors)).toEqual([
      "report_id",
      "$.expected_result",
      "Steps[0].ActualResult",
      "attachments[0].file_name",
    ]);
    expect(parseApiError(error)).toMatchObject({
      status: 400,
      code: "model_state_validation_error",
    });
  });

  it("problem+json без charset и с Content-Type в другом регистре — тот же результат", async () => {
    const error = await failedRequest(
      respondWithError(400, validationProblem, {
        "Content-Type": "application/problem+json",
      })
    );

    const body = (error as AxiosError).response
      ?.data as typeof validationProblem;
    expect(Object.keys(body.errors)).toEqual([
      "report_id",
      "$.expected_result",
      "Steps[0].ActualResult",
      "attachments[0].file_name",
    ]);
  });

  /**
   * ДЕФЕКТ 1 (документирующий тест, поведение сегодня корректно по итогу, но не по причине).
   *
   * Конвертацию кейсов делает второй response-интерцептор, зарегистрированный
   * ТОЛЬКО с fulfilled-обработчиком (`base.ts:77`). Ответы 4xx/5xx идут по
   * rejected-ветке и до него не доходят вовсе — значит guard
   * `isProblemDetailsResponse` (`base.ts:81`) на телах ошибок никогда не
   * исполняется. Инвариант «ключи errors не переименовываются» держится не
   * границей problem+json, а тем, что тела ошибок не конвертируются в принципе.
   *
   * Доказательство: ответ 400 с обычным `application/json` и snake-ключами тоже
   * остаётся неконвертированным.
   */
  it("граница problem+json не участвует в пути ошибок: 400 с application/json тоже не конвертируется", async () => {
    const error = await failedRequest(
      respondWithError(
        400,
        { report_title: "x", error_list: [{ error: "a", reason: "b" }] },
        { "content-type": "application/json" }
      )
    );

    expect((error as AxiosError).response?.data).toEqual({
      report_title: "x",
      error_list: [{ error: "a", reason: "b" }],
    });
  });

  it("успешный ответ по-прежнему конвертируется — регрессии в основном потоке нет", async () => {
    const instance = createApiInstance();
    instance.defaults.adapter = async (config) => ({
      data: { report_title: "Падает карточка" },
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    });

    const response = await instance.get("/api/v2/reports");

    expect(response.data).toEqual({ reportTitle: "Падает карточка" });
  });
});

describe("путь ошибки: битые, пустые и не-JSON тела", () => {
  it("оборванный problem+json не роняет интерцептор — тело остаётся строкой", async () => {
    const truncated = '{"type":"urn:bugget:error:report_not_found","deta';
    const error = await failedRequest(
      respondWithError(400, truncated, {
        "content-type": "application/problem+json",
      })
    );

    expect((error as AxiosError).response?.data).toBe(truncated);
    expect(parseApiError(error)).toEqual({ status: 400 });
  });

  it("non-JSON 401 от внешнего nginx доезжает до потребителя без вторичной ошибки", async () => {
    const error = await failedRequest(
      respondWithError(
        401,
        "<html><head><title>401 Authorization Required</title></head></html>",
        { "content-type": "text/html" }
      )
    );

    expect(parseApiError(error)).toEqual({ status: 401 });
  });

  it("пустое тело 403 не роняет интерцептор", async () => {
    const error = await failedRequest(respondWithError(403, "", {}));

    expect(parseApiError(error)).toEqual({ status: 403 });
  });

  it("500 с html-телом проходит ветку логирования интерцептора без исключения", async () => {
    const error = await failedRequest(
      respondWithError(500, "<html>502 Bad Gateway</html>", {
        "content-type": "text/html",
      })
    );

    expect(parseApiError(error)).toEqual({ status: 500 });
  });

  it('тело-строка JSON (BadRequest("текст") у merge) не даёт ни code, ни message', async () => {
    const error = await failedRequest(
      respondWithError(400, "Нельзя объединить аккаунт сам с собой", {
        "content-type": "application/json",
      })
    );

    expect(parseApiError(error)).toEqual({ status: 400 });
  });

  it("сетевая ошибка без response не роняет интерцептор", async () => {
    const instance = createApiInstance();
    instance.defaults.adapter = async (config) => {
      throw new AxiosError("Network Error", AxiosError.ERR_NETWORK, config);
    };

    const error = await failedRequest(instance);

    expect(parseApiError(error)).toEqual({ status: undefined });
  });
});

describe("путь ошибки: ветвление source_owns_workspaces", () => {
  /** Реальный провод сегодня: `Conflict(new { error = errorCode })`, UsersController.cs:200. */
  it("legacy 409 {error} даёт status и code, на которые ветвится Settings", async () => {
    const error = await failedRequest(
      respondWithError(
        409,
        { error: "source_owns_workspaces" },
        { "content-type": "application/json" }
      )
    );

    const { status, code } = parseApiError(error);
    expect(status === 409 && code === "source_owns_workspaces").toBe(true);
  });

  it("целевая форма Problem Details даёт то же ветвление", async () => {
    const error = await failedRequest(
      respondWithError(
        409,
        {
          type: "urn:bugget:error:source_owns_workspaces",
          title: "Аккаунт владеет рабочей областью",
          status: 409,
          detail: "У второго аккаунта есть рабочая область",
          code: "source_owns_workspaces",
        },
        { "content-type": "application/problem+json; charset=utf-8" }
      )
    );

    const { status, code } = parseApiError(error);
    expect(status === 409 && code === "source_owns_workspaces").toBe(true);
  });

  it("problem+json без code, но с type — ветвление сохраняется", async () => {
    const error = await failedRequest(
      respondWithError(
        409,
        {
          type: "urn:bugget:error:source_owns_workspaces",
          status: 409,
        },
        { "content-type": "application/problem+json" }
      )
    );

    const { status, code } = parseApiError(error);
    expect(status === 409 && code === "source_owns_workspaces").toBe(true);
  });
});

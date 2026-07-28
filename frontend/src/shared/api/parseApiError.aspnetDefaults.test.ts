import { describe, expect, it } from "vitest";
import { parseApiError } from "./parseApiError";

/**
 * Adversarial-проверка MAIN-66: парсер против ФАКТИЧЕСКИХ дефолтов ASP.NET Core,
 * а не против придуманных Problem Details.
 *
 * Значения ниже сняты запуском `ProblemDetailsFactory` и `ApiBehaviorOptions.
 * ClientErrorMapping` на net9.0 (та же версия, что в `Directory.Build.props`),
 * а не взяты из головы:
 *
 *   400 → title 'Bad Request',      type 'https://tools.ietf.org/html/rfc9110#section-15.5.1'
 *   405 → title 'Method Not Allowed', type '…#section-15.5.6'
 *   500 → title 'An error occurred while processing your request.', type '…#section-15.6.1'
 *   ValidationProblemDetails → title 'One or more validation errors occurred.',
 *                              type '…#section-15.5.1', detail отсутствует, code отсутствует
 *
 * Такие ответы попадают на провод везде, где кастомная фабрика из слайса 2 не
 * отработала: 405/406/415 от пайплайна MVC, необработанное 500, дефолтная
 * валидация `[ApiController]`. Толерантный парсер обязан переживать именно их.
 *
 * Тесты `it.fails` фиксируют дефект: сейчас они «проходят», потому что ожидание
 * не выполняется. После починки они станут красными — это и есть сигнал снять пин.
 */

const axiosError = (status: number, data: unknown) => ({
  isAxiosError: true,
  response: { status, data },
});

/** Дефолтный ValidationProblemDetails ASP.NET: ни code, ни detail, английский title. */
const aspNetValidationProblem = {
  type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  title: "One or more validation errors occurred.",
  status: 400,
  errors: { Title: ["Поле обязательно"] },
};

describe("parseApiError против дефолтов ASP.NET: title", () => {
  it("ДЕФЕКТ 2 (факт): title дефолтного 500 попадает в message русского тоста", () => {
    const result = parseApiError(
      axiosError(500, {
        type: "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        title: "An error occurred while processing your request.",
        status: 500,
      })
    );

    // Список HTTP_REASON_PHRASES содержит 'internal server error', а ASP.NET
    // такой строки не отдаёт — фильтр не срабатывает.
    expect(result.message).toBe(
      "An error occurred while processing your request."
    );
  });

  it.fails(
    "ДЕФЕКТ 2 (ожидание): английский технический title дефолтного 500 не должен доезжать до тоста",
    () => {
      const result = parseApiError(
        axiosError(500, {
          type: "https://tools.ietf.org/html/rfc9110#section-15.6.1",
          title: "An error occurred while processing your request.",
          status: 500,
        })
      );

      expect(result.message).toBeUndefined();
    }
  );

  it("ДЕФЕКТ 2 (факт): статусы вне списка отдают reason phrase как есть", () => {
    // 405/406/408/412/426 в HTTP_REASON_PHRASES отсутствуют.
    expect(
      parseApiError(axiosError(405, { title: "Method Not Allowed" })).message
    ).toBe("Method Not Allowed");
    expect(
      parseApiError(axiosError(408, { title: "Request Timeout" })).message
    ).toBe("Request Timeout");
  });

  it("ДЕФЕКТ 2 (факт): дефолтная валидация показывает пользователю английскую фразу", () => {
    const result = parseApiError(axiosError(400, aspNetValidationProblem));

    expect(result.message).toBe("One or more validation errors occurred.");
  });

  it("контроль: статусы из списка отфильтрованы корректно", () => {
    expect(
      parseApiError(axiosError(404, { title: "Not Found" })).message
    ).toBeUndefined();
    expect(
      parseApiError(axiosError(409, { title: "Conflict" })).message
    ).toBeUndefined();
    expect(
      parseApiError(axiosError(400, { title: "Bad Request" })).message
    ).toBeUndefined();
  });

  it("контроль: осмысленный русский title по-прежнему доезжает", () => {
    expect(
      parseApiError(axiosError(409, { title: "Команда уже существует" }))
        .message
    ).toBe("Команда уже существует");
  });
});

describe("parseApiError против дефолтов ASP.NET: code", () => {
  it("ДЕФЕКТ 3 (факт): из ссылки на RFC собирается несуществующий машинный код", () => {
    const result = parseApiError(axiosError(400, aspNetValidationProblem));

    expect(result.code).toBe("rfc9110#section-15.5.1");
  });

  it.fails(
    "ДЕФЕКТ 3 (ожидание): type, который не является urn:bugget:error:<code>, не должен давать code",
    () => {
      const result = parseApiError(axiosError(400, aspNetValidationProblem));

      expect(result.code).toBeUndefined();
    }
  );

  it("ДЕФЕКТ 3 (факт): about:blank из RFC 9457 превращается в code 'blank'", () => {
    // RFC 9457 §3.1.1: если конкретного типа нет, type равен 'about:blank'.
    const result = parseApiError(
      axiosError(403, { type: "about:blank", status: 403 })
    );

    expect(result.code).toBe("blank");
  });

  it.fails("ДЕФЕКТ 3 (ожидание): about:blank не несёт машинного кода", () => {
    const result = parseApiError(
      axiosError(403, { type: "about:blank", status: 403 })
    );

    expect(result.code).toBeUndefined();
  });

  it("контроль: целевой urn:bugget:error:<code> разбирается верно", () => {
    expect(
      parseApiError(
        axiosError(404, { type: "urn:bugget:error:report_not_found" })
      ).code
    ).toBe("report_not_found");
  });
});

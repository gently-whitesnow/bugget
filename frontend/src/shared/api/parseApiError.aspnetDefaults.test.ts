import { describe, expect, it } from "vitest";
import { parseApiError } from "./parseApiError";

// MAIN-66: тела ниже — фактические дефолты ASP.NET Core net9.0
// (`ProblemDetailsFactory`, `ClientErrorMapping`), а не придуманные: они
// попадают на провод там, где кастомная фабрика не отработала.

const axiosError = (status: number, data: unknown) => ({
  isAxiosError: true,
  response: { status, data },
});

const aspNetValidationProblem = {
  type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  title: "One or more validation errors occurred.",
  status: 400,
  errors: { Title: ["Поле обязательно"] },
};

describe("parseApiError против дефолтов ASP.NET: title", () => {
  it("не показывает title дефолтного 500 в русском тосте", () => {
    const result = parseApiError(
      axiosError(500, {
        type: "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        title: "An error occurred while processing your request.",
        status: 500,
      })
    );

    expect(result.message).toBeUndefined();
  });

  it("не привязан к вручную перечисленным HTTP-статусам", () => {
    expect(
      parseApiError(
        axiosError(405, {
          type: "https://tools.ietf.org/html/rfc9110#section-15.5.6",
          title: "Method Not Allowed",
        })
      ).message
    ).toBeUndefined();
    expect(
      parseApiError(
        axiosError(408, {
          type: "https://tools.ietf.org/html/rfc9110#section-15.5.9",
          title: "Request Timeout",
        })
      ).message
    ).toBeUndefined();
  });

  it("не показывает title дефолтной валидации", () => {
    const result = parseApiError(axiosError(400, aspNetValidationProblem));

    expect(result.message).toBeUndefined();
  });

  it("контроль: осмысленный русский title по-прежнему доезжает", () => {
    expect(
      parseApiError(
        axiosError(409, {
          type: "urn:bugget:error:team_exists",
          title: "Команда уже существует",
        })
      ).message
    ).toBe("Команда уже существует");
  });
});

describe("parseApiError против дефолтов ASP.NET: code", () => {
  it("не извлекает машинный код из ссылки на RFC", () => {
    const result = parseApiError(axiosError(400, aspNetValidationProblem));

    expect(result.code).toBeUndefined();
  });

  it("не извлекает машинный код из about:blank", () => {
    // RFC 9457 §3.1.1: если конкретного типа нет, type равен 'about:blank'.
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

import { describe, expect, it } from "vitest";
import { parseApiError } from "./parseApiError";

/** Ошибка axios в том виде, в каком её ловит catch: важны только status/data. */
const axiosError = (status: number, data: unknown) => ({
  isAxiosError: true,
  response: { status, data },
});

describe("parseApiError: Problem Details (RFC 9457)", () => {
  it("читает code и detail целевой формы", () => {
    const result = parseApiError(
      axiosError(409, {
        type: "urn:bugget:error:source_owns_workspaces",
        title: "Аккаунт владеет рабочей областью",
        status: 409,
        detail: "У второго аккаунта есть рабочая область",
        instance: "/api/users/v1/users/merge",
        code: "source_owns_workspaces",
      })
    );

    expect(result).toEqual({
      status: 409,
      code: "source_owns_workspaces",
      detail: "У второго аккаунта есть рабочая область",
      title: "Аккаунт владеет рабочей областью",
      message: "У второго аккаунта есть рабочая область",
    });
  });

  it("берёт код из хвоста type, если code потерян", () => {
    const result = parseApiError(
      axiosError(404, {
        type: "urn:bugget:error:report_not_found",
        status: 404,
      })
    );

    expect(result.code).toBe("report_not_found");
  });

  it("подставляет в message title, когда detail не пришёл", () => {
    const result = parseApiError(
      axiosError(400, { title: "Команда уже существует", code: "team_exists" })
    );

    expect(result.message).toBe("Команда уже существует");
  });

  it("отбрасывает title, который повторяет стандартный HTTP reason phrase", () => {
    const result = parseApiError(
      axiosError(404, { title: "Not Found", status: 404, code: "not_found" })
    );

    expect(result.title).toBeUndefined();
    expect(result.message).toBeUndefined();
  });

  it("не трогает ключи словаря errors — их читает вызывающий код как есть", () => {
    const body = {
      status: 400,
      code: "model_state_validation_error",
      errors: { report_title: ["Поле обязательно"] },
    };

    expect(parseApiError(axiosError(400, body)).code).toBe(
      "model_state_validation_error"
    );
    expect(body.errors).toEqual({ report_title: ["Поле обязательно"] });
  });
});

describe("parseApiError: legacy {error, reason}", () => {
  it("читает error как code и reason как detail", () => {
    const result = parseApiError(
      axiosError(409, {
        error: "source_owns_workspaces",
        reason: "У второго аккаунта есть рабочая область",
      })
    );

    expect(result).toEqual({
      status: 409,
      code: "source_owns_workspaces",
      detail: "У второго аккаунта есть рабочая область",
      title: undefined,
      message: "У второго аккаунта есть рабочая область",
    });
  });

  it("переживает тело без reason", () => {
    const result = parseApiError(axiosError(409, { error: "user_conflict" }));

    expect(result.code).toBe("user_conflict");
    expect(result.message).toBeUndefined();
  });

  it("предпочитает code и detail, если пришли обе формы сразу", () => {
    const result = parseApiError(
      axiosError(400, {
        error: "legacy_code",
        reason: "legacy reason",
        code: "problem_code",
        detail: "problem detail",
      })
    );

    expect(result.code).toBe("problem_code");
    expect(result.detail).toBe("problem detail");
  });
});

describe("parseApiError: не-JSON, пустые и битые данные", () => {
  it("переживает non-JSON 401 от внешнего nginx", () => {
    const result = parseApiError(
      axiosError(401, "<html><body>401 Authorization Required</body></html>")
    );

    expect(result).toEqual({ status: 401 });
  });

  it("переживает пустое тело", () => {
    expect(parseApiError(axiosError(403, ""))).toEqual({ status: 403 });
    expect(parseApiError(axiosError(403, null))).toEqual({ status: 403 });
    expect(parseApiError(axiosError(204, undefined))).toEqual({ status: 204 });
  });

  it("переживает массив вместо объекта в теле", () => {
    expect(parseApiError(axiosError(400, [{ error: "x" }]))).toEqual({
      status: 400,
    });
  });

  it("игнорирует поля неверных типов и пустые строки", () => {
    const result = parseApiError(
      axiosError(500, { code: 42, detail: "   ", title: null, reason: false })
    );

    expect(result).toEqual({
      status: 500,
      code: undefined,
      detail: undefined,
      title: undefined,
      message: undefined,
    });
  });

  it("переживает сетевую ошибку без response", () => {
    expect(parseApiError(new Error("Network Error"))).toEqual({
      status: undefined,
    });
  });

  it("переживает вход, который вообще не похож на ошибку", () => {
    expect(parseApiError(undefined)).toEqual({ status: undefined });
    expect(parseApiError("boom")).toEqual({ status: undefined });
    expect(parseApiError({ response: "not an object" })).toEqual({
      status: undefined,
    });
  });
});

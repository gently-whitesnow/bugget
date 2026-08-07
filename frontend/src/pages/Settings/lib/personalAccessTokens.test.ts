import { describe, expect, it } from "vitest";
import {
  formatTokenDate,
  isTokenExpired,
  isTokenOutOfCurrentTeam,
} from "./personalAccessTokens";

describe("область действия токена", () => {
  it("чужая команда опознаётся при разных типах идентификатора", () => {
    expect(isTokenOutOfCurrentTeam({ teamId: 7 }, "2")).toBe(true);
    expect(isTokenOutOfCurrentTeam({ teamId: 2 }, "2")).toBe(false);
    expect(isTokenOutOfCurrentTeam({ teamId: 2 }, 2)).toBe(false);
  });

  it("без контекста команды пометки нет: сравнивать не с чем", () => {
    expect(isTokenOutOfCurrentTeam({ teamId: 7 }, null)).toBe(false);
    expect(isTokenOutOfCurrentTeam({ teamId: 7 }, undefined)).toBe(false);
    expect(isTokenOutOfCurrentTeam({ teamId: 7 }, "")).toBe(false);
  });
});

describe("срок жизни токена", () => {
  const now = Date.parse("2026-08-07T00:00:00Z");

  it("истёкший и ещё живой различаются по границе", () => {
    expect(isTokenExpired({ expiresAt: "2026-08-06T23:59:59Z" }, now)).toBe(
      true
    );
    expect(isTokenExpired({ expiresAt: "2026-08-07T00:00:01Z" }, now)).toBe(
      false
    );
  });

  it("момент истечения считается наступившим", () => {
    expect(isTokenExpired({ expiresAt: "2026-08-07T00:00:00Z" }, now)).toBe(
      true
    );
  });

  it("токен без срока не истекает", () => {
    expect(isTokenExpired({ expiresAt: null }, now)).toBe(false);
  });

  it("неразбираемая дата не выдаётся за истёкшую", () => {
    expect(isTokenExpired({ expiresAt: "не дата" }, now)).toBe(false);
  });
});

describe("формат даты", () => {
  it("дата приводится к короткой русской записи", () => {
    expect(formatTokenDate("2026-08-07T10:00:00Z")).toContain("2026");
  });
});

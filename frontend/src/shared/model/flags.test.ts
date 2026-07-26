import { describe, expect, it, vi, beforeEach } from "vitest";
import { allSettled, fork } from "effector";
import { $flags, fetchFlagsFx } from "./flags";

vi.mock("@/shared/api/instances/authorization", () => ({
  authorizationApi: {
    get: vi.fn(),
  },
  authorizationPath: (path: string) => `/api/authorization/v1${path}`,
}));

describe("flags model", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("store defaults to betaTest=false", () => {
    const scope = fork();
    expect(scope.getState($flags)).toEqual({
      betaTest: false,
    });
  });

  it("updates store when fetchFlagsFx succeeds with betaTest=true", async () => {
    const scope = fork({
      handlers: [[fetchFlagsFx, () => Promise.resolve({ betaTest: true })]],
    });

    await allSettled(fetchFlagsFx, { scope });

    expect(scope.getState($flags)).toEqual({
      betaTest: true,
    });
  });

  it("keeps default when fetchFlagsFx fails", async () => {
    const scope = fork({
      handlers: [[fetchFlagsFx, () => Promise.reject(new Error("403"))]],
    });

    await allSettled(fetchFlagsFx, { scope });

    expect(scope.getState($flags)).toEqual({
      betaTest: false,
    });
  });
});

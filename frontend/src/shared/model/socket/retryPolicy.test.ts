import { describe, expect, it } from "vitest";

import { createRetryPolicy, nextReconnectDelay } from "./retryPolicy";

// random = 0.5 → нулевой jitter, удобно проверять «чистый» экспоненциальный ряд
const noJitter = () => 0.5;

describe("nextReconnectDelay", () => {
  it("retries immediately on the first attempt", () => {
    expect(nextReconnectDelay(0, noJitter)).toBe(0);
  });

  it("grows exponentially until the cap", () => {
    const delays = [1, 2, 3, 4, 5].map((count) =>
      nextReconnectDelay(count, noJitter)
    );

    expect(delays).toEqual([1_000, 2_000, 4_000, 8_000, 16_000]);
  });

  it("caps the delay at 30s and never gives up", () => {
    const delays = [6, 10, 50, 1_000].map((count) =>
      nextReconnectDelay(count, noJitter)
    );

    expect(delays).toEqual([30_000, 30_000, 30_000, 30_000]);
  });

  it("applies jitter within ±20% of the base delay", () => {
    // previousRetryCount=3 → 4000ms, jitter даёт диапазон [3200, 4800]
    expect(nextReconnectDelay(3, () => 0)).toBe(3_200);
    expect(nextReconnectDelay(3, () => 1)).toBe(4_800);
  });
});

describe("createRetryPolicy", () => {
  it("never returns null so SignalR keeps reconnecting forever", () => {
    const policy = createRetryPolicy();

    const delays = [0, 1, 7, 100, 10_000].map((previousRetryCount) =>
      policy.nextRetryDelayInMilliseconds({
        previousRetryCount,
        elapsedMilliseconds: previousRetryCount * 30_000,
        retryReason: new Error("connection lost"),
      })
    );

    delays.forEach((delay) => {
      expect(delay).not.toBeNull();
      expect(delay).toBeTypeOf("number");
    });
  });

  it("stays within the capped range", () => {
    const policy = createRetryPolicy();

    for (
      let previousRetryCount = 0;
      previousRetryCount < 50;
      previousRetryCount++
    ) {
      const delay = policy.nextRetryDelayInMilliseconds({
        previousRetryCount,
        elapsedMilliseconds: 0,
        retryReason: new Error("connection lost"),
      }) as number;

      expect(delay).toBeGreaterThanOrEqual(0);
      expect(delay).toBeLessThanOrEqual(36_000);
    }
  });
});

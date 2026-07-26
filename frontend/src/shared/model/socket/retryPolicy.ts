import type { IRetryPolicy, RetryContext } from "@microsoft/signalr";

const baseDelay = 1_000;
const maxDelay = 30_000;
const jitterRatio = 0.2;

/**
 * Пауза между попытками переподключения: сначала сразу, потом 1s, 2s, 4s,
 * 8s, 16s и дальше всегда 30s. Попытки не прекращаются никогда — вкладка
 * может провисеть в фоне часами, и после возвращения пользователя
 * соединение должно подняться само.
 */
export const nextReconnectDelay = (
  previousRetryCount: number,
  random: () => number = Math.random
): number => {
  if (previousRetryCount <= 0) return 0;

  const exponential = Math.min(
    baseDelay * 2 ** (previousRetryCount - 1),
    maxDelay
  );

  // ±20% чтобы толпа вкладок не ломилась на сервер одновременно
  const jitter = exponential * jitterRatio * (random() * 2 - 1);

  return Math.round(exponential + jitter);
};

export const createRetryPolicy = (): IRetryPolicy => ({
  nextRetryDelayInMilliseconds: (retryContext: RetryContext) =>
    nextReconnectDelay(retryContext.previousRetryCount),
});

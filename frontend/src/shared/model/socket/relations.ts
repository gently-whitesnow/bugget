import { sample } from "effector";

import {
  $isConnected,
  $isOnline,
  $isRecoveryInProgress,
  $recoveryId,
  $revivalAttempts,
  $wasDisconnected,
  appWokeUp,
  browserWentOnline,
  connectionClosed,
  connectionReconnecting,
  connectionRecoveryStarted,
  connectionRestored,
  connectionStarted,
  initSocketFx,
  longSleepDetected,
  reconnectStuckDetected,
  restartSocketFx,
  waitForReconnectStuckFx,
  waitBeforeRevivalFx,
} from "./model";
import { nextReconnectDelay } from "./retryPolicy";

/**
 * Соединение закрылось окончательно (SignalR сдался или упал старт) — поднимаем
 * его заново. Без этого вкладка живёт с мёртвым сокетом до перезагрузки.
 * Пауза растёт с каждой попыткой, пока соединение не станет стабильным.
 */
sample({
  clock: connectionClosed,
  source: { attempts: $revivalAttempts, isOnline: $isOnline },
  filter: ({ isOnline }) => isOnline,
  fn: ({ attempts }) => nextReconnectDelay(attempts),
  target: waitBeforeRevivalFx,
});

sample({
  clock: waitBeforeRevivalFx.done,
  source: { isOnline: $isOnline, isConnected: $isConnected },
  filter: ({ isOnline, isConnected }) => isOnline && !isConnected,
  fn: () => undefined,
  target: initSocketFx,
});

/**
 * Пользователь вернулся на вкладку или сеть поднялась — пробуем сразу, не
 * дожидаясь таймера: в фоне браузер душит setTimeout, и очередная попытка
 * может быть отложена на минуту.
 */
sample({
  clock: appWokeUp,
  source: { isOnline: $isOnline, isConnected: $isConnected },
  filter: ({ isOnline, isConnected }) => isOnline && !isConnected,
  target: restartSocketFx,
});

// Возврат сети означает, что предыдущий WebSocket больше нельзя считать живым.
sample({
  clock: browserWentOnline,
  source: $isOnline,
  filter: Boolean,
  target: restartSocketFx,
});

// После долгого сна состояние HubConnection может ошибочно остаться Connected.
sample({
  clock: longSleepDetected,
  source: $isOnline,
  filter: Boolean,
  target: restartSocketFx,
});

/** Первый сигнал потери связи открывает единый двухминутный цикл восстановления. */
sample({
  clock: connectionReconnecting,
  target: connectionRecoveryStarted,
});

sample({
  clock: connectionClosed,
  source: $isRecoveryInProgress,
  filter: (isRecoveryInProgress) => !isRecoveryInProgress,
  target: connectionRecoveryStarted,
});

sample({
  clock: connectionRecoveryStarted,
  source: $recoveryId,
  target: waitForReconnectStuckFx,
});

sample({
  clock: waitForReconnectStuckFx.doneData,
  source: {
    recoveryId: $recoveryId,
    isRecoveryInProgress: $isRecoveryInProgress,
  },
  filter: ({ recoveryId, isRecoveryInProgress }, completedRecoveryId) =>
    isRecoveryInProgress && recoveryId === completedRecoveryId,
  target: reconnectStuckDetected,
});

/**
 * Соединение поднялось после разрыва. Сбрасываем флаг не по connectionStarted,
 * а по самому connectionRestored — иначе чтение и сброс стора шли бы от одного
 * клока с неопределённым порядком.
 */
sample({
  clock: connectionStarted,
  source: $wasDisconnected,
  filter: Boolean,
  fn: () => undefined,
  target: connectionRestored,
});

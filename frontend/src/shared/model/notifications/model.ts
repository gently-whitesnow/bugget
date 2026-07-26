import { createEffect, createEvent, createStore, sample } from "effector";
import {
  Notification,
  NotificationAction,
  NotificationType,
  NotifyErrorOptions,
} from "./types";

const maxToasts = 5;

const defaultTtlByType: Record<Exclude<NotificationType, "error">, number> = {
  success: 3000,
  info: 4000,
  warning: 6000,
};

const createNotificationId = (): string => {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

export const getDefaultTtlMs = (
  type: NotificationType,
  ttlMs?: number
): number | undefined => {
  if (typeof ttlMs === "number") {
    return ttlMs;
  }

  if (type === "error") {
    return undefined;
  }

  return defaultTtlByType[type];
};

export type NotifyPayload = Omit<Notification, "id" | "count">;

type NotifySuccessPayload = {
  title: string;
  message?: string;
};

type NotifyErrorPayload = {
  title: string;
  message?: string;
  options?: NotifyErrorOptions;
};

export const notifyRequested = createEvent<NotifyPayload>();
export const dismissRequested = createEvent<string>();
export const clearAllRequested = createEvent();
export const setDegradedModeRequested = createEvent<string>();
export const clearDegradedModeRequested = createEvent();
export const notifySuccessRequested = createEvent<NotifySuccessPayload>();
export const notifyErrorRequested = createEvent<NotifyErrorPayload>();

export const createNotificationFx = createEffect<NotifyPayload, Notification>(
  (notification) => ({
    ...notification,
    id: createNotificationId(),
    ttlMs: getDefaultTtlMs(notification.type, notification.ttlMs),
  })
);

const notificationAdded = createEvent<Notification>();

sample({
  clock: notifyRequested,
  target: createNotificationFx,
});

sample({
  clock: createNotificationFx.doneData,
  target: notificationAdded,
});

sample({
  clock: notifySuccessRequested,
  fn: ({ title, message }) => ({
    type: "success" as const,
    title,
    message,
  }),
  target: notifyRequested,
});

sample({
  clock: notifyErrorRequested,
  fn: ({ title, message, options }) => {
    const actions: NotificationAction[] | undefined = options?.retry
      ? [
          {
            label: "Попробовать снова",
            onClick: options.retry,
            kind: "outline",
          },
          ...(options.actions ?? []),
        ]
      : options?.actions;

    return {
      type: "error" as const,
      title,
      message,
      dedupeKey: options?.dedupeKey,
      ttlMs: options?.ttlMs,
      actions,
    };
  },
  target: notifyRequested,
});

export const $notifications = createStore<Notification[]>([])
  .on(notificationAdded, (notifications, incoming) => {
    const dedupeKey = incoming.dedupeKey;

    if (dedupeKey) {
      const existingIndex = notifications.findIndex(
        (item) => item.dedupeKey === dedupeKey
      );

      if (existingIndex >= 0) {
        const existing = notifications[existingIndex];
        const merged: Notification = {
          ...existing,
          ...incoming,
          id: existing.id,
          count: (existing.count ?? 1) + 1,
        };

        const withoutExisting = notifications.filter(
          (_, index) => index !== existingIndex
        );

        return [merged, ...withoutExisting].slice(0, maxToasts);
      }
    }

    return [{ ...incoming, count: 1 }, ...notifications].slice(0, maxToasts);
  })
  .on(dismissRequested, (notifications, id) =>
    notifications.filter((item) => item.id !== id)
  )
  .reset(clearAllRequested);

export const $degradedModeMessage = createStore<string | null>(null)
  .on(setDegradedModeRequested, (_, message) => message)
  .reset(clearDegradedModeRequested);

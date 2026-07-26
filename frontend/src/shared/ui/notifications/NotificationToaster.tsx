import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNotifications } from "@/shared/model";
import { ToastNotificationItem } from "./components/ToastNotificationItem";

type TimerEntry = {
  timeoutId: number;
  startedAt: number;
  remainingMs: number;
  signature: string;
};

const exitAnimationMs = 180;

export const NotificationToaster = () => {
  const { notifications, dismiss } = useNotifications();
  const [closingIds, setClosingIds] = useState<Set<string>>(new Set());
  const timerMapRef = useRef<Map<string, TimerEntry>>(new Map());
  const closeTimeoutsRef = useRef<Map<string, number>>(new Map());

  const clearTimer = useCallback((id: string) => {
    const entry = timerMapRef.current.get(id);
    if (!entry) {
      return;
    }

    window.clearTimeout(entry.timeoutId);
    timerMapRef.current.delete(id);
  }, []);

  const clearCloseTimeout = useCallback((id: string) => {
    const timeoutId = closeTimeoutsRef.current.get(id);
    if (!timeoutId) {
      return;
    }

    window.clearTimeout(timeoutId);
    closeTimeoutsRef.current.delete(id);
  }, []);

  const startClose = useCallback(
    (id: string) => {
      clearTimer(id);
      clearCloseTimeout(id);

      setClosingIds((prev) => {
        const next = new Set(prev);
        next.add(id);
        return next;
      });

      const timeoutId = window.setTimeout(() => {
        dismiss(id);
        closeTimeoutsRef.current.delete(id);
        setClosingIds((prev) => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      }, exitAnimationMs);

      closeTimeoutsRef.current.set(id, timeoutId);
    },
    [clearCloseTimeout, clearTimer, dismiss]
  );

  const startTimer = useCallback(
    (id: string, timeoutMs: number, signature: string) => {
      clearTimer(id);

      const timeoutId = window.setTimeout(() => {
        startClose(id);
      }, timeoutMs);

      timerMapRef.current.set(id, {
        timeoutId,
        startedAt: Date.now(),
        remainingMs: timeoutMs,
        signature,
      });
    },
    [clearTimer, startClose]
  );

  const pauseTimer = useCallback((id: string) => {
    const entry = timerMapRef.current.get(id);
    if (!entry) {
      return;
    }

    window.clearTimeout(entry.timeoutId);
    const elapsed = Date.now() - entry.startedAt;

    timerMapRef.current.set(id, {
      ...entry,
      timeoutId: 0,
      startedAt: Date.now(),
      remainingMs: Math.max(0, entry.remainingMs - elapsed),
    });
  }, []);

  const resumeTimer = useCallback(
    (id: string) => {
      const entry = timerMapRef.current.get(id);
      if (!entry || entry.timeoutId !== 0) {
        return;
      }

      if (entry.remainingMs <= 0) {
        startClose(id);
        return;
      }

      startTimer(id, entry.remainingMs, entry.signature);
    },
    [startClose, startTimer]
  );

  useEffect(() => {
    const existingIds = new Set(notifications.map((item) => item.id));

    timerMapRef.current.forEach((_, id) => {
      if (!existingIds.has(id) || closingIds.has(id)) {
        clearTimer(id);
      }
    });

    notifications.forEach((notification) => {
      if (closingIds.has(notification.id)) {
        return;
      }

      if (!notification.ttlMs || notification.ttlMs <= 0) {
        clearTimer(notification.id);
        return;
      }

      const signature = [
        notification.ttlMs,
        notification.count ?? 1,
        notification.title,
        notification.message ?? "",
      ].join("|");

      const timer = timerMapRef.current.get(notification.id);

      if (!timer || timer.signature !== signature) {
        startTimer(notification.id, notification.ttlMs, signature);
      }
    });

    setClosingIds((prev) => {
      let hasChanges = false;
      const next = new Set(prev);

      prev.forEach((id) => {
        if (!existingIds.has(id)) {
          next.delete(id);
          hasChanges = true;
        }
      });

      return hasChanges ? next : prev;
    });
  }, [notifications, closingIds, clearTimer, startTimer]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") {
        return;
      }

      const latestVisibleNotification = notifications.find(
        (item) => !closingIds.has(item.id)
      );

      if (latestVisibleNotification) {
        startClose(latestVisibleNotification.id);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [closingIds, notifications, startClose]);

  useEffect(() => {
    const timerMap = timerMapRef.current;
    const closeTimeouts = closeTimeoutsRef.current;

    return () => {
      timerMap.forEach((entry) => {
        window.clearTimeout(entry.timeoutId);
      });
      timerMap.clear();

      closeTimeouts.forEach((timeoutId) => {
        window.clearTimeout(timeoutId);
      });
      closeTimeouts.clear();
    };
  }, []);

  const renderedNotifications = useMemo(
    () =>
      notifications.map((notification) => {
        const isClosing = closingIds.has(notification.id);

        return (
          <ToastNotificationItem
            key={notification.id}
            notification={notification}
            isClosing={isClosing}
            onMouseEnter={() => pauseTimer(notification.id)}
            onMouseLeave={() => resumeTimer(notification.id)}
            onClose={() => startClose(notification.id)}
          />
        );
      }),
    [closingIds, notifications, pauseTimer, resumeTimer, startClose]
  );

  if (notifications.length === 0) {
    return null;
  }

  return (
    <div className="toast toast-bottom toast-end bottom-2 z-[120] gap-2 max-w-md pointer-events-none">
      {renderedNotifications}
    </div>
  );
};

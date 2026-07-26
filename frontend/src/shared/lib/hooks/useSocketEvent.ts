import { useEffect, useRef } from "react";
import { SocketEvent, SocketPayload } from "@/shared/model";
import { watchSocketEvents } from "@/shared/model";

/**
 * Хук для подписки на конкретное серверное событие.
 */
export function useSocketEvent<E extends SocketEvent>(
  type: E,
  handler: (payload: SocketPayload[E]) => void
) {
  const handlerRef = useRef(handler);

  useEffect(() => {
    handlerRef.current = handler;
  }, [handler]);

  useEffect(() => {
    const unsub = watchSocketEvents((evt) => {
      if (evt.type === type) {
        handlerRef.current(evt.payload as SocketPayload[E]);
      }
    });
    return () => unsub();
  }, [type]);
}

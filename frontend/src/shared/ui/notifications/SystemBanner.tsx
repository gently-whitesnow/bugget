import { useUnit } from "effector-react";
import {
  $reconnectStuck,
  $socketConnectionStatus,
  SocketConnectionStatus,
} from "@/shared/model/socket";
import { useNetworkStatus } from "./useNetworkStatus";
import { useNotifications } from "@/shared/model";

type BannerState = {
  key: "offline" | "websocket" | "websocket-stuck" | "degraded";
  message: string;
  /** Показать кнопку «Обновить страницу» */
  shouldShowReloadButton?: boolean;
};

type GetBannerStateParams = {
  isOnline: boolean;
  socketConnectionStatus: SocketConnectionStatus;
  reconnectStuck: boolean;
  degradedModeMessage: string | null;
};

const getBannerState = ({
  isOnline,
  socketConnectionStatus,
  reconnectStuck,
  degradedModeMessage,
}: GetBannerStateParams): BannerState | null => {
  if (!isOnline) {
    return {
      key: "offline",
      message: "Нет интернета. Попробуем переподключиться…",
    };
  }

  if (reconnectStuck) {
    return {
      key: "websocket-stuck",
      message:
        "Не удаётся восстановить соединение — данные могут быть неактуальны.",
      shouldShowReloadButton: true,
    };
  }

  if (socketConnectionStatus === SocketConnectionStatus.DISCONNECTED) {
    return {
      key: "websocket",
      message: "Соединение потеряно. Переподключаемся…",
    };
  }

  if (degradedModeMessage) {
    return {
      key: "degraded",
      message: degradedModeMessage,
    };
  }

  return null;
};

export const SystemBanner = () => {
  const isOnline = useNetworkStatus();
  const [socketConnectionStatus, reconnectStuck] = useUnit([
    $socketConnectionStatus,
    $reconnectStuck,
  ]);
  const { degradedModeMessage } = useNotifications();
  const banner = getBannerState({
    isOnline,
    socketConnectionStatus,
    reconnectStuck,
    degradedModeMessage,
  });

  if (!banner) {
    return null;
  }

  return (
    <div
      key={banner.key}
      className="shrink-0 z-[110] alert alert-soft alert-warning rounded-none shadow-sm backdrop-blur-sm transition-opacity duration-200 motion-reduce:transition-none"
      role="status"
    >
      <span>{banner.message}</span>
      {banner.shouldShowReloadButton && (
        <button
          type="button"
          className="btn btn-xs btn-warning"
          onClick={() => window.location.reload()}
        >
          Обновить страницу
        </button>
      )}
    </div>
  );
};

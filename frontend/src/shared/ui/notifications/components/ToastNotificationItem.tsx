import { Notification, NotificationAction } from "@/shared/model";

type Props = {
  notification: Notification;
  isClosing: boolean;
  onMouseEnter: () => void;
  onMouseLeave: () => void;
  onClose: () => void;
};

const getActionKindClass = (kind: NotificationAction["kind"]) => {
  switch (kind) {
    case "primary":
      return "btn-primary";
    case "outline":
      return "btn-outline";
    case "ghost":
    default:
      return "btn-ghost";
  }
};

const getAlertTypeClass = (type: "success" | "error" | "warning" | "info") => {
  switch (type) {
    case "success":
      return "border-success/45 bg-success/20 text-base-content";
    case "error":
      return "border-error/45 bg-error/20 text-base-content";
    case "warning":
      return "border-warning/50 bg-warning/24 text-base-content";
    case "info":
    default:
      return "border-info/45 bg-info/20 text-base-content";
  }
};

export const ToastNotificationItem = ({
  notification,
  isClosing,
  onMouseEnter,
  onMouseLeave,
  onClose,
}: Props) => {
  const hasCountBadge = (notification.count ?? 1) > 1;

  return (
    <div
      className={[
        "pointer-events-auto w-full",
        "transition-all duration-200 ease-out motion-reduce:transition-none",
        isClosing
          ? "translate-x-6 opacity-0 motion-reduce:translate-x-0"
          : "translate-x-0 opacity-100",
      ].join(" ")}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
    >
      <div
        className={[
          "w-full border p-4",
          getAlertTypeClass(notification.type),
          "shadow-lg rounded-box backdrop-blur-sm",
        ].join(" ")}
        role="status"
      >
        <div className="w-full">
          <div className="flex items-start justify-between gap-2">
            <div className="leading-tight">{notification.title}</div>
            <div className="flex items-center gap-2">
              {hasCountBadge && (
                <span className="badge badge-sm badge-neutral">
                  x{notification.count}
                </span>
              )}
              <button
                className="btn btn-ghost btn-xs"
                type="button"
                onClick={onClose}
                aria-label="Dismiss notification"
              >
                ✕
              </button>
            </div>
          </div>

          {notification.message && (
            <div className="mt-1 text-sm leading-snug">
              {notification.message}
            </div>
          )}

          {notification.actions && notification.actions.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {notification.actions.map((action, index) => (
                <button
                  key={`${notification.id}-${action.label}-${index}`}
                  className={[
                    "btn btn-xs font-normal",
                    getActionKindClass(action.kind ?? "ghost"),
                  ].join(" ")}
                  style={{ borderWidth: "1px" }}
                  type="button"
                  onClick={action.onClick}
                >
                  {action.label}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

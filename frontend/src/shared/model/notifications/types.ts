export type NotificationType = "success" | "error" | "warning" | "info";

export type NotificationAction = {
  label: string;
  onClick: () => void;
  kind?: "primary" | "outline" | "ghost";
};

export type Notification = {
  id: string;
  type: NotificationType;
  title: string;
  message?: string;
  ttlMs?: number;
  actions?: NotificationAction[];
  dedupeKey?: string;
  count?: number;
};

export type NotifyErrorOptions = {
  dedupeKey?: string;
  ttlMs?: number;
  retry?: () => void;
  actions?: NotificationAction[];
};

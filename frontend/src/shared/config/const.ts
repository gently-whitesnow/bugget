import { StatusMeta } from "@/shared/lib/types";
import {
  CircleDashed,
  Clock,
  CircleCheck,
  XCircle,
  ThumbsUp,
  Bug,
  GitCommitVertical,
  FlaskConical,
} from "lucide-react";

export enum BugStatuses {
  OPEN = 0,
  VERIFIED = 1,
  REJECTED = 2,
  FIXED = 3,
}

export enum ReportStatuses {
  BACKLOG = 0,
  RESOLVED = 1,
  FIX = 2,
  REJECTED = 3,
  TEST = 4,
}

export enum RequestStates {
  IDLE = 0,
  PENDING = 1,
  DONE = 2,
  ERROR = 3,
}

export enum AttachmentTypes {
  FACT = 0,
  EXPECT = 1,
  COMMENT = 2,
  BUG_STEP = 3,
}

export const reportStatusMap: Record<number, StatusMeta> = {
  [ReportStatuses.FIX]: {
    title: "Фикс",
    borderColor: "border-error",
    icon: Clock,
    iconColor: "text-warning",
  },
  [ReportStatuses.TEST]: {
    title: "Тест",
    borderColor: "border-info",
    icon: FlaskConical,
    iconColor: "text-info",
  },
  [ReportStatuses.RESOLVED]: {
    title: "Решён",
    borderColor: "border-success",
    icon: CircleCheck,
    iconColor: "text-success",
  },
  [ReportStatuses.REJECTED]: {
    title: "Отклонён",
    borderColor: "border-secondary",
    icon: XCircle,
    iconColor: "text-secondary",
  },
  [ReportStatuses.BACKLOG]: {
    title: "Бэклог",
    borderColor: "border-neutral",
    icon: CircleDashed,
    iconColor: "text-base-content",
  },
};

export const bugStatusMap: Record<number, StatusMeta> = {
  [BugStatuses.OPEN]: {
    title: "Открыт",
    borderColor: "",
    icon: Bug,
    iconColor: "text-error",
  },
  [BugStatuses.FIXED]: {
    title: "Исправлен",
    borderColor: "border-info",
    icon: GitCommitVertical,
    iconColor: "text-info",
  },
  [BugStatuses.VERIFIED]: {
    title: "Проверен",
    borderColor: "border-success",
    icon: ThumbsUp,
    iconColor: "text-success",
  },
  [BugStatuses.REJECTED]: {
    title: "Отклонён",
    borderColor: "border-secondary",
    icon: XCircle,
    iconColor: "text-secondary",
  },
};

export enum BugResultTypes {
  RECEIVE = "receive",
  EXPECT = "expect",
}

export enum SettingTypes {
  WORKSPACE = "workspace",
  TEAM = "team",
  USER = "user",
}

export enum CreatorTypes {
  USER = 0,
  SYSTEM = 1,
  TG_BETA_TESTER = 2,
}

export enum CommentAudiences {
  INTERNAL = 0,
  EXTERNAL = 1,
}

export enum BootstrapStatus {
  NO_WORKSPACE = "no-workspace",
  NO_TEAM = "no-team",
  READY = "ready",
}

export const justNowString = "только что";
export const yesterdayString = "вчера";
export const backInTimeString = "назад";

export const lastReportsDashboardTake = 5;

export const activeReportStatuses = [
  ReportStatuses.BACKLOG,
  ReportStatuses.FIX,
  ReportStatuses.TEST,
] as const;

/** Высота хэдера — используется в дочерних компонентах Layout для адаптации к скрытию хедера */
export const headerHeight = "4rem";

/** Лимиты символов для текстовых полей (синхронизированы с бэкендом) */
export const resultMaxLength = 2048;
export const commentMaxLength = 2048;
export const bugStepMaxLength = 1024;
export const linkMaxLength = 2048;
export const linkNameMaxLength = 256;

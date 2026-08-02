import {
  bugStatusMap,
  reportStatusMap,
  type BugStatuses,
  type ReportStatuses,
} from "@/shared/config";
import { StatusMeta } from "@/shared/lib/types";
import { MessageCircleQuestion } from "lucide-react";

type EntityType = "bug" | "report";

const unknownStatus: StatusMeta = {
  title: "Неизвестно",
  borderColor: "border-neutral",
  icon: MessageCircleQuestion,
  iconColor: "text-neutral",
};

/**
 * `status` типизирован значением провода, но приходит из ответа сервера, а не из
 * кода: `?? unknownStatus` остаётся защитой от значения, которого фронт ещё не
 * знает, — показать «Неизвестно» честнее, чем чужой статус.
 */
export default function getStatusMeta(
  type: EntityType,
  status: ReportStatuses | BugStatuses
): StatusMeta {
  if (type === "report")
    return reportStatusMap[status as ReportStatuses] ?? unknownStatus;
  if (type === "bug")
    return bugStatusMap[status as BugStatuses] ?? unknownStatus;
  return unknownStatus;
}

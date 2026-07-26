import { bugStatusMap, reportStatusMap } from "@/shared/config";
import { StatusMeta } from "@/shared/lib/types";
import { MessageCircleQuestion } from "lucide-react";

type EntityType = "bug" | "report";

const unknownStatus: StatusMeta = {
  title: "Неизвестно",
  borderColor: "border-neutral",
  icon: MessageCircleQuestion,
  iconColor: "text-neutral",
};

export default function getStatusMeta(
  type: EntityType,
  status: number
): StatusMeta {
  if (type === "report") return reportStatusMap[status] ?? unknownStatus;
  if (type === "bug") return bugStatusMap[status] ?? unknownStatus;
  return unknownStatus;
}

import { BugStatuses, bugStatusMap } from "@/shared/config";
import { EntityStatusOption } from "@/shared/ui";

const bugStatusActiveClassNames: Record<BugStatuses, string> = {
  [BugStatuses.OPEN]: "bg-error/10",
  [BugStatuses.FIXED]: "bg-info/10",
  [BugStatuses.VERIFIED]: "bg-success/10",
  [BugStatuses.REJECTED]: "bg-secondary/10",
};

export const bugStatusOptions: EntityStatusOption<BugStatuses>[] = [
  BugStatuses.OPEN,
  BugStatuses.FIXED,
  BugStatuses.VERIFIED,
  BugStatuses.REJECTED,
].map((status) => ({
  value: status,
  label: bugStatusMap[status].title,
  icon: bugStatusMap[status].icon,
  iconClassName: bugStatusMap[status].iconColor,
  activeClassName: bugStatusActiveClassNames[status],
}));

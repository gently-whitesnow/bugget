import { reportStatusMap, ReportStatuses } from "@/shared/config";
import { DropdownOption } from "@/shared/ui";
import { X } from "lucide-react";
import type { MouseEventHandler } from "react";

type StatusFilterOption = {
  value: ReportStatuses;
  badgeClassName: string;
};

type Props = {
  option: DropdownOption<ReportStatuses>;
  onRemove: MouseEventHandler<HTMLButtonElement>;
  statusOptions: StatusFilterOption[];
};

const StatusDropdownToken = ({ option, onRemove, statusOptions }: Props) => {
  const statusMeta = reportStatusMap[option.value];
  const styleOption = statusOptions.find((item) => item.value === option.value);
  const Icon = statusMeta.icon;

  return (
    <span
      className={`badge h-7 gap-1.5 border pl-2.5 pr-1 ${styleOption?.badgeClassName ?? "border-base-content/15 bg-base-content/10 text-base-content"}`}
    >
      <Icon className={`h-3.5 w-3.5 ${statusMeta.iconColor}`} />
      <span>{statusMeta.title}</span>
      <button
        type="button"
        onMouseDown={onRemove}
        onClick={onRemove}
        aria-label={`Убрать статус ${statusMeta.title}`}
        className="inline-flex h-5 w-5 cursor-pointer items-center justify-center rounded-full hover:bg-base-100/40"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </span>
  );
};

export default StatusDropdownToken;

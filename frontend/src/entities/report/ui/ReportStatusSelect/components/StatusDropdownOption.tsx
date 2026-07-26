import { reportStatusMap, ReportStatuses } from "@/shared/config";
import { DropdownOption, StatusIndicator } from "@/shared/ui";
import { Check } from "lucide-react";

type Props = {
  option: DropdownOption<ReportStatuses>;
  isSelected: boolean;
};

const StatusDropdownOption = ({ option, isSelected }: Props) => {
  const statusMeta = reportStatusMap[option.value];

  return (
    <div className="flex items-center justify-between">
      <StatusIndicator statusMeta={statusMeta} />
      <Check
        className={`h-4 w-4 ${isSelected ? "text-success" : "text-transparent"}`}
      />
    </div>
  );
};

export default StatusDropdownOption;

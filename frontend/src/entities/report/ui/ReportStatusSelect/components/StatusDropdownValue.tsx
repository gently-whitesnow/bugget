import { reportStatusMap, ReportStatuses } from "@/shared/config";
import { DropdownOption, StatusIndicator } from "@/shared/ui";

type Props = {
  selectedOptions: DropdownOption<ReportStatuses>[];
  multiple?: boolean;
};

const StatusDropdownValue = ({ selectedOptions, multiple = false }: Props) => {
  if (selectedOptions.length === 0) {
    return <span className="text-base-content/50">Любой статус</span>;
  }

  const firstStatusMeta = reportStatusMap[selectedOptions[0].value];

  return (
    <div className="flex min-w-0 items-center gap-2">
      <StatusIndicator statusMeta={firstStatusMeta} />
      {multiple && selectedOptions.length > 1 && (
        <span className="text-xs text-base-content/60">
          +{selectedOptions.length - 1}
        </span>
      )}
    </div>
  );
};

export default StatusDropdownValue;

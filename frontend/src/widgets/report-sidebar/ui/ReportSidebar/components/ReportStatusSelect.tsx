import { useUnit } from "effector-react";
import { ReportStatusSelect as ReportStatusDropdown } from "@/entities/report";
import { $statusStore, changeStatusEvent } from "@/entities/report";

const ReportStatusSelect = () => {
  const status = useUnit($statusStore);
  const changeStatus = useUnit(changeStatusEvent);

  return (
    <ReportStatusDropdown
      value={status}
      onChange={changeStatus}
      className="w-full"
    />
  );
};

export default ReportStatusSelect;

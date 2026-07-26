import { BugStatuses } from "@/shared/config";
import { EntityStatusSelect } from "@/shared/ui";

import { bugStatusOptions } from "./bugStatusOptions";

type Props = {
  status: BugStatuses;
  onChange: (status: BugStatuses) => void;
  className?: string;
};

const BugStatusSelect = ({ status, onChange, className }: Props) => {
  return (
    <EntityStatusSelect
      status={status}
      options={bugStatusOptions}
      onChange={onChange}
      className={className}
      menuClassName="w-max min-w-full"
    />
  );
};

export default BugStatusSelect;

import { ReportStatuses, reportStatusMap } from "@/shared/config";
import {
  DropdownOption,
  EntityStatusOption,
  EntityStatusSelect,
  MultipleDropdown,
} from "@/shared/ui";

import StatusDropdownOption from "./components/StatusDropdownOption";
import StatusDropdownToken from "./components/StatusDropdownToken";
import StatusDropdownValue from "./components/StatusDropdownValue";

type CommonProps = {
  className?: string;
  label?: string;
};

type SingleProps = CommonProps & {
  multiple?: false;
  value: ReportStatuses;
  onChange: (value: ReportStatuses) => void;
};

type MultiProps = CommonProps & {
  multiple: true;
  value: ReportStatuses[] | null;
  onChange: (value: ReportStatuses[] | null) => void;
};

type Props = SingleProps | MultiProps;

type StatusFilterOption = {
  value: ReportStatuses;
  badgeClassName: string;
  activeClassName: string;
};

const statusOptions: StatusFilterOption[] = [
  {
    value: ReportStatuses.BACKLOG,
    badgeClassName:
      "border-base-content/15 bg-base-content/10 text-base-content",
    activeClassName: "bg-base-content/10",
  },
  {
    value: ReportStatuses.FIX,
    badgeClassName: "border-warning/25 bg-warning/15 text-base-content",
    activeClassName: "bg-warning/15",
  },
  {
    value: ReportStatuses.TEST,
    badgeClassName: "border-info/25 bg-info/15 text-base-content",
    activeClassName: "bg-info/15",
  },
  {
    value: ReportStatuses.RESOLVED,
    badgeClassName: "border-success/25 bg-success/15 text-base-content",
    activeClassName: "bg-success/15",
  },
  {
    value: ReportStatuses.REJECTED,
    badgeClassName: "border-secondary/25 bg-secondary/15 text-base-content",
    activeClassName: "bg-secondary/15",
  },
];

const dropdownOptions: DropdownOption<ReportStatuses>[] = statusOptions.map(
  (option) => ({
    label: reportStatusMap[option.value].title,
    value: option.value,
  })
);

const singleOptions: EntityStatusOption<ReportStatuses>[] = statusOptions.map(
  (option) => ({
    value: option.value,
    label: reportStatusMap[option.value].title,
    icon: reportStatusMap[option.value].icon,
    iconClassName: reportStatusMap[option.value].iconColor,
    activeClassName: option.activeClassName,
  })
);

const ReportStatusSelect = (props: Props) => {
  const { className, label } = props;
  const multiple = props.multiple === true;
  const value = props.value;

  const handleChange = (
    nextValue: ReportStatuses | ReportStatuses[] | null
  ) => {
    if (props.multiple === true) {
      const list = Array.isArray(nextValue) ? nextValue : [];
      props.onChange(list.length > 0 ? list : null);
      return;
    }

    if (!Array.isArray(nextValue) && nextValue !== null) {
      props.onChange(nextValue);
    }
  };

  if (!multiple) {
    return (
      <EntityStatusSelect
        status={value}
        options={singleOptions}
        onChange={handleChange}
        className={className}
        fullWidth={true}
      />
    );
  }

  return (
    <MultipleDropdown
      label={label}
      multiple={multiple}
      value={value}
      onChange={handleChange}
      options={dropdownOptions}
      placeholder="Любой статус"
      className={className}
      renderValue={(selectedOptions) => (
        <StatusDropdownValue
          selectedOptions={selectedOptions}
          multiple={multiple}
        />
      )}
      renderOption={(option, isSelected) => (
        <StatusDropdownOption option={option} isSelected={isSelected} />
      )}
      renderToken={
        multiple
          ? (option, onRemove) => (
              <StatusDropdownToken
                option={option}
                onRemove={onRemove}
                statusOptions={statusOptions}
              />
            )
          : undefined
      }
    />
  );
};

export default ReportStatusSelect;

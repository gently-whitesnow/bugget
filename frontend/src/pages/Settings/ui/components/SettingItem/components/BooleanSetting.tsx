import { SettingLabel } from "./SettingLabel";

type Props = {
  title: string;
  description?: string | null;
  isChecked: boolean;
  onChange: (checked: boolean) => void;
  isUpdating: boolean;
};

export const BooleanSetting = ({
  title,
  description,
  isChecked,
  onChange,
  isUpdating,
}: Props) => {
  return (
    <div className="flex items-center justify-between py-4 border-b border-base-300/50 last:border-b-0">
      <SettingLabel title={title} description={description} />
      <label className="cursor-pointer flex-shrink-0 ml-4">
        <input
          type="checkbox"
          className="toggle toggle-primary"
          checked={isChecked}
          onChange={(e) => onChange(e.target.checked)}
          disabled={isUpdating}
        />
      </label>
    </div>
  );
};

import { SettingLabel } from "./SettingLabel";
import { SaveCancelButtons } from "./SaveCancelButtons";

type Props = {
  title: string;
  description?: string;
  value: string;
  hasChanges: boolean;
  isEditing: boolean;
  isUpdating: boolean;
  onChange: (value: string) => void;
  onSave: () => void;
  onCancel: () => void;
};

export const TextSetting = ({
  title,
  description,
  value,
  hasChanges,
  isEditing,
  isUpdating,
  onChange,
  onSave,
  onCancel,
}: Props) => {
  return (
    <div className="py-4 border-b border-base-300/50 last:border-b-0">
      <div className="flex items-start justify-between mb-2">
        <SettingLabel title={title} description={description} />
        {isEditing && hasChanges && (
          <SaveCancelButtons
            onSave={onSave}
            onCancel={onCancel}
            isUpdating={isUpdating}
          />
        )}
      </div>
      <input
        type="text"
        className="input input-bordered w-full"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={() => {
          if (hasChanges && isEditing) {
            onSave();
          }
        }}
        disabled={isUpdating}
      />
    </div>
  );
};

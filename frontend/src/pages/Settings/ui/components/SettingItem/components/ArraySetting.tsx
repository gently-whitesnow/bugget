import { useState, useCallback } from "react";
import { Plus, X } from "lucide-react";
import { SettingLabel } from "./SettingLabel";
import { SaveCancelButtons } from "./SaveCancelButtons";

type Props = {
  title: string;
  description?: string;
  values: string[];
  hasChanges: boolean;
  isEditing: boolean;
  isUpdating: boolean;
  onValuesChange: (values: string[]) => void;
  onSave: () => void;
  onCancel: () => void;
  setIsEditing: (editing: boolean) => void;
};

export const ArraySetting = ({
  title,
  description,
  values,
  hasChanges,
  isEditing,
  isUpdating,
  onValuesChange,
  onSave,
  onCancel,
  setIsEditing,
}: Props) => {
  const [newValue, setNewValue] = useState("");

  const handleAdd = useCallback(() => {
    if (newValue.trim()) {
      onValuesChange([...values, newValue.trim()]);
      setNewValue("");
      setIsEditing(true);
    }
  }, [newValue, values, onValuesChange, setIsEditing]);

  const handleRemove = useCallback(
    (index: number) => {
      onValuesChange(values.filter((_, i) => i !== index));
      setIsEditing(true);
    },
    [values, onValuesChange, setIsEditing]
  );

  return (
    <div className="py-4 border-b border-base-300/50 last:border-b-0">
      <div className="flex items-start justify-between mb-3">
        <SettingLabel title={title} description={description} />
        {isEditing && hasChanges && (
          <SaveCancelButtons
            onSave={onSave}
            onCancel={onCancel}
            isUpdating={isUpdating}
          />
        )}
      </div>

      <div className="flex flex-wrap gap-2 mb-3">
        {values.map((value, index) => (
          <div
            key={index}
            className="flex items-center gap-1 px-3 py-1.5 bg-base-200 rounded-lg text-sm group"
          >
            <span>{value}</span>
            <button
              className="opacity-0 group-hover:opacity-100 transition-opacity ml-1 hover:text-error"
              onClick={() => handleRemove(index)}
              disabled={isUpdating}
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          type="text"
          className="input input-bordered input-sm flex-1"
          placeholder="Добавить значение..."
          value={newValue}
          onChange={(e) => setNewValue(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              handleAdd();
            }
          }}
          disabled={isUpdating}
        />
        <button
          className="btn btn-ghost btn-sm"
          onClick={handleAdd}
          disabled={!newValue.trim() || isUpdating}
        >
          <Plus className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
};

import { useEffect, useState, useCallback, useMemo } from "react";
import type { SettingView } from "../../../api/contracts";
import { BooleanSetting } from "./components/BooleanSetting";
import { ArraySetting } from "./components/ArraySetting";
import { TextSetting } from "./components/TextSetting";
import { getCustomSettingComponent } from "./components/customSettings";

type Props = {
  setting: SettingView;
  sectionId: string;
  onUpdate: (sectionId: string, settingId: string, values: string[]) => void;
  isUpdating: boolean;
  readOnly?: boolean;
};

export const SettingItem = ({
  setting,
  sectionId,
  onUpdate,
  isUpdating,
  readOnly = false,
}: Props) => {
  const [localValues, setLocalValues] = useState<string[]>(
    setting.values ?? []
  );
  const [isEditing, setIsEditing] = useState(false);

  useEffect(() => {
    setLocalValues(setting.values ?? []);
  }, [setting.values]);

  const settingValues = useMemo(() => setting.values ?? [], [setting.values]);
  const hasChanges =
    JSON.stringify(localValues) !== JSON.stringify(settingValues);

  const handleSave = useCallback(() => {
    if (hasChanges) {
      onUpdate(sectionId, setting.id, localValues);
      setIsEditing(false);
    }
  }, [sectionId, setting.id, localValues, hasChanges, onUpdate]);

  const handleCancel = useCallback(() => {
    setLocalValues(settingValues);
    setIsEditing(false);
  }, [settingValues]);

  const disabled = isUpdating || readOnly;

  // Проверяем наличие кастомного компонента
  const CustomComponent = getCustomSettingComponent(sectionId, setting.id);
  if (CustomComponent) {
    const handleCustomSave = (values: string[]) => {
      onUpdate(sectionId, setting.id, values);
    };

    return (
      <CustomComponent
        title={setting.title}
        description={setting.description}
        values={settingValues}
        isUpdating={disabled}
        onSave={handleCustomSave}
      />
    );
  }

  // Булевая настройка
  if (setting.isBool) {
    const isChecked = localValues?.[0]?.toLowerCase() === "true";

    const handleBoolChange = (checked: boolean) => {
      const newValues = [checked ? "true" : "false"];
      setLocalValues(newValues);
      onUpdate(sectionId, setting.id, newValues);
    };

    return (
      <BooleanSetting
        title={setting.title}
        description={setting.description}
        isChecked={isChecked}
        onChange={handleBoolChange}
        isUpdating={disabled}
      />
    );
  }

  // Массив значений
  if (setting.isArray) {
    return (
      <ArraySetting
        title={setting.title}
        description={setting.description}
        values={localValues}
        hasChanges={hasChanges}
        isEditing={isEditing}
        isUpdating={disabled}
        onValuesChange={setLocalValues}
        onSave={handleSave}
        onCancel={handleCancel}
        setIsEditing={setIsEditing}
      />
    );
  }

  // Текстовая настройка
  const handleTextChange = (value: string) => {
    setLocalValues([value]);
    setIsEditing(true);
  };

  return (
    <TextSetting
      title={setting.title}
      description={setting.description}
      value={localValues?.[0] ?? ""}
      hasChanges={hasChanges}
      isEditing={isEditing}
      isUpdating={disabled}
      onChange={handleTextChange}
      onSave={handleSave}
      onCancel={handleCancel}
    />
  );
};

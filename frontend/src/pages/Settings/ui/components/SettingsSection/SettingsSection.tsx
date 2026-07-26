import { useCallback } from "react";
import type { SettingsSectionView } from "../../../api/contracts";
import type { SettingType } from "../../../model/types";
import { SettingItem } from "../SettingItem/SettingItem";

type Props = {
  section: SettingsSectionView;
  type: SettingType;
  onUpdate: (
    type: SettingType,
    sectionId: string,
    settingId: string,
    values: string[]
  ) => void;
  isUpdating: boolean;
  readOnly?: boolean;
};

export const SettingsSection = ({
  section,
  type,
  onUpdate,
  isUpdating,
  readOnly = false,
}: Props) => {
  const handleSettingUpdate = useCallback(
    (sectionId: string, settingId: string, values: string[]) => {
      onUpdate(type, sectionId, settingId, values);
    },
    [type, onUpdate]
  );

  return (
    <div className="bg-base-100 rounded-xl border border-base-300/50 overflow-hidden w-full">
      <div className="px-5 py-4 bg-base-200/50 border-b border-base-300/50">
        <h3 className="font-semibold text-base-content">{section.title}</h3>
      </div>
      <div className="px-5 w-full">
        {section.settings.length === 0 ? (
          <div className="py-8 text-center text-base-content/50 text-sm">
            Нет настроек в этой секции
          </div>
        ) : (
          section.settings.map((setting) => (
            <SettingItem
              key={setting.id}
              setting={setting}
              sectionId={section.id}
              onUpdate={handleSettingUpdate}
              isUpdating={isUpdating}
              readOnly={readOnly}
            />
          ))
        )}
      </div>
    </div>
  );
};

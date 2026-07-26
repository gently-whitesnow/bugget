import { Settings as SettingsIcon } from "lucide-react";

export const SettingsHeader = () => {
  return (
    <div className="flex items-center gap-3">
      <div className="p-2.5 bg-primary/10 rounded-xl">
        <SettingsIcon className="w-6 h-6 text-primary" />
      </div>
      <div>
        <h1 className="text-2xl font-bold text-base-content">Настройки</h1>
        <p className="text-sm text-base-content/60">
          Управление настройками приложения
        </p>
      </div>
    </div>
  );
};

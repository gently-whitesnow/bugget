import { Building2, Users, User } from "lucide-react";
import { SettingTypes } from "@/shared/config";
import type { SettingType } from "../../../model/types";

type TabConfig = {
  id: SettingType;
  title: string;
  icon: typeof Building2;
};

const tabs: TabConfig[] = [
  {
    id: SettingTypes.WORKSPACE,
    title: "Рабочее пространство",
    icon: Building2,
  },
  { id: SettingTypes.TEAM, title: "Команда", icon: Users },
  { id: SettingTypes.USER, title: "Пользователь", icon: User },
];

type Props = {
  activeTab: SettingType;
  onTabChange: (tab: SettingType) => void;
};

export const SettingsTabs = ({ activeTab, onTabChange }: Props) => {
  return (
    <div className="-mx-[var(--layout-page-padding-inline)] overflow-x-auto px-[var(--layout-page-padding-inline)]">
      <div className="flex w-max min-w-full gap-1 rounded-xl bg-base-200 p-1">
        {tabs.map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              className={`flex shrink-0 items-center gap-2 whitespace-nowrap rounded-lg px-4 py-2.5 text-sm font-medium transition-all ${
                isActive
                  ? "bg-base-100 text-base-content shadow-sm"
                  : "text-base-content/60 hover:bg-base-100/50 hover:text-base-content"
              }`}
              onClick={() => onTabChange(tab.id)}
            >
              <Icon className="h-4 w-4" />
              {tab.title}
            </button>
          );
        })}
      </div>
    </div>
  );
};

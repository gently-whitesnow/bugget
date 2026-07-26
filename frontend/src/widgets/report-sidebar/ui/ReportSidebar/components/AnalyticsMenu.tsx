import { useUnit } from "effector-react";
import { Check, Dot, Square } from "lucide-react";
import ActionDropdown, { ActionItem } from "@/shared/ui/ActionDropdown";
import {
  $isPending,
  toggleExcludeFromAnalyticsFx,
} from "../../../model/excludeFromAnalytics";

type Props = {
  reportId: number;
  value: boolean;
  onChange: (next: boolean) => void;
};

const AnalyticsMenu = ({ reportId, value, onChange }: Props) => {
  const [isPending, toggle] = useUnit([
    $isPending,
    toggleExcludeFromAnalyticsFx,
  ]);

  const handleToggle = async () => {
    if (isPending) return;
    const next = !value;
    onChange(next);
    try {
      await toggle({ reportId, value: next });
    } catch {
      onChange(!next);
    }
  };

  const items: ActionItem[] = [
    {
      icon: value ? (
        <Check className="w-4 h-4" />
      ) : (
        <Square className="w-4 h-4" />
      ),
      label: "Исключить из аналитики",
      onClick: handleToggle,
    },
  ];

  return (
    <ActionDropdown
      items={items}
      triggerIcon={<Dot className="w-5 h-5" />}
      menuPosition="end"
    />
  );
};

export default AnalyticsMenu;

import { LucideIcon } from "lucide-react";

type Props = {
  label: string;
  description?: string;
  icon: LucideIcon;
  iconClassName: string;
  compact?: boolean;
  showDescription?: boolean;
  truncateLabel?: boolean;
};

const StatusOptionContent = ({
  label,
  description,
  icon: Icon,
  iconClassName,
  compact = false,
  showDescription = true,
  truncateLabel = true,
}: Props) => {
  return (
    <div className="flex min-w-0 items-center gap-2">
      <Icon
        className={`shrink-0 ${
          compact ? "h-4 w-4" : "h-4 w-4"
        } ${iconClassName}`}
      />
      <div className="min-w-0">
        <div
          className={`${truncateLabel ? "truncate" : "whitespace-nowrap"} ${
            compact ? "text-sm font-medium" : "text-sm"
          }`}
        >
          {label}
        </div>
        {showDescription && description && (
          <div
            className={`${
              truncateLabel ? "truncate" : "whitespace-nowrap"
            } text-base-content/60 ${
              compact ? "text-[11px] leading-4" : "text-xs"
            }`}
          >
            {description}
          </div>
        )}
      </div>
    </div>
  );
};

export default StatusOptionContent;

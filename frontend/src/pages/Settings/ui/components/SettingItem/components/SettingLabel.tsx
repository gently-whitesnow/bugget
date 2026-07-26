type Props = {
  title: string;
  description?: string;
};

export const SettingLabel = ({ title, description }: Props) => {
  return (
    <div className="flex-1 min-w-0">
      <div className="font-medium text-base-content">{title}</div>
      {description && (
        <div className="text-sm text-base-content/60 mt-0.5 break-words">
          {description}
        </div>
      )}
    </div>
  );
};

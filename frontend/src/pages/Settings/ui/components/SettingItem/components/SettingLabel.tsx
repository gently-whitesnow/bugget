type Props = {
  title: string;
  /**
   * `null` — законное значение с провода: в контракте `Setting.description`
   * обязателен по присутствию ключа и nullable по значению. Рендер этого и так
   * не различал, тип теперь говорит правду.
   */
  description?: string | null;
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

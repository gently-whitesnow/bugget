// Пропсы которые получает каждый кастомный компонент настройки
export type CustomSettingProps = {
  title: string;
  description?: string | null;
  values: string[];
  isUpdating: boolean;
  onSave: (values: string[]) => void;
};

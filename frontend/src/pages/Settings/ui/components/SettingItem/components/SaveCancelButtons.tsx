import { X, Check } from "lucide-react";

type Props = {
  onSave: () => void;
  onCancel: () => void;
  isUpdating: boolean;
};

export const SaveCancelButtons = ({ onSave, onCancel, isUpdating }: Props) => {
  return (
    <div className="flex gap-1">
      <button
        className="btn btn-ghost btn-xs"
        onClick={onCancel}
        disabled={isUpdating}
      >
        <X className="w-3 h-3" />
      </button>
      <button
        className="btn btn-primary btn-xs"
        onClick={onSave}
        disabled={isUpdating}
      >
        {isUpdating ? (
          <span className="loading loading-spinner loading-xs" />
        ) : (
          <Check className="w-3 h-3" />
        )}
      </button>
    </div>
  );
};

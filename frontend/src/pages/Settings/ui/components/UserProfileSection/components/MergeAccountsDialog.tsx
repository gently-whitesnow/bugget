type Props = {
  isMerging: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

export const MergeAccountsDialog = ({
  isMerging,
  onConfirm,
  onCancel,
}: Props) => {
  return (
    <div className="modal modal-open" onClick={onCancel}>
      <div
        className="modal-box relative max-w-md"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="font-bold text-lg">Объединение аккаунтов</h3>
        <p className="py-4 text-sm text-base-content/80">
          Этот аккаунт уже привязан к другому пользователю. Хотите объединить
          аккаунты? Все привязки будут перенесены на текущий аккаунт.
        </p>
        <div className="modal-action">
          <button
            className="btn btn-ghost btn-sm"
            onClick={onCancel}
            disabled={isMerging}
          >
            Отмена
          </button>
          <button
            className="btn btn-primary btn-sm"
            onClick={onConfirm}
            disabled={isMerging}
          >
            {isMerging ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              "Объединить"
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

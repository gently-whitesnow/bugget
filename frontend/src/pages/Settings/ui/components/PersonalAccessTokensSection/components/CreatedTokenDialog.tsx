import { useCallback, useState } from "react";
import { copyToClipboard } from "@/shared/lib";

type Props = {
  token: string;
  onClose: () => void;
};

/**
 * Единственный показ значения токена. Клик мимо окна его не закрывает: в
 * хранилище остался хэш, и случайно закрытое окно означает потерянный токен.
 */
export const CreatedTokenDialog = ({ token, onClose }: Props) => {
  const [isCopied, setIsCopied] = useState(false);

  const handleCopy = useCallback(async () => {
    try {
      await copyToClipboard(token);
      setIsCopied(true);
    } catch (error) {
      console.error("Failed to copy personal access token", error);
    }
  }, [token]);

  return (
    <div className="modal modal-open">
      <div className="modal-box relative max-w-lg">
        <h3 className="font-bold text-lg">Токен выпущен</h3>
        <p className="py-4 text-sm text-base-content/80">
          Скопируйте значение сейчас — оно показывается один раз, повторно
          посмотреть его будет негде.
        </p>
        <code className="block break-all rounded-lg bg-base-200 p-3 font-mono text-sm">
          {token}
        </code>
        <div className="modal-action">
          <button className="btn btn-ghost btn-sm" onClick={handleCopy}>
            {isCopied ? "Скопировано" : "Скопировать"}
          </button>
          <button className="btn btn-primary btn-sm" onClick={onClose}>
            Готово
          </button>
        </div>
      </div>
    </div>
  );
};

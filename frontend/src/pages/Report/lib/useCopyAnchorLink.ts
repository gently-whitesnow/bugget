import { useCallback } from "react";
import { useUnit } from "effector-react";
import { notifyErrorRequested, notifySuccessRequested } from "@/shared/model";
import { copyToClipboard } from "@/shared/lib/clipboard";

type Params = {
  anchorHref: string;
  successTitle?: string;
  errorTitle?: string;
  errorLogLabel?: string;
};

export const useCopyAnchorLink = ({
  anchorHref,
  successTitle = "Ссылка скопирована",
  errorTitle = "Не удалось скопировать ссылку",
  errorLogLabel = "Failed to copy anchor link",
}: Params) => {
  const notifySuccess = useUnit(notifySuccessRequested);
  const notifyError = useUnit(notifyErrorRequested);

  return useCallback(async () => {
    const anchorLink = new URL(anchorHref, window.location.href).href;

    try {
      await copyToClipboard(anchorLink);
      notifySuccess({ title: successTitle });
    } catch (error) {
      console.error(`${errorLogLabel}:`, error);
      notifyError({ title: errorTitle });
    }
  }, [
    anchorHref,
    errorLogLabel,
    errorTitle,
    notifyError,
    notifySuccess,
    successTitle,
  ]);
};

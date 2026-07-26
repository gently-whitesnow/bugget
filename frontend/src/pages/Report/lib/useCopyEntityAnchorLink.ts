import { useMemo } from "react";
import { useCopyAnchorLink } from "./useCopyAnchorLink";

type AnchorHrefBuilder = (parentId: number, entityId: number) => string;

type Params = {
  parentId: number;
  entityId: number;
  getAnchorHref: AnchorHrefBuilder;
  successTitle?: string;
  errorTitle?: string;
  errorLogLabel?: string;
};

export const useCopyEntityAnchorLink = ({
  parentId,
  entityId,
  getAnchorHref,
  successTitle = "Ссылка скопирована",
  errorTitle = "Не удалось скопировать ссылку",
  errorLogLabel = "Failed to copy anchor link",
}: Params) => {
  const anchorHref = useMemo(
    () => getAnchorHref(parentId, entityId),
    [entityId, getAnchorHref, parentId]
  );

  return useCopyAnchorLink({
    anchorHref,
    successTitle,
    errorTitle,
    errorLogLabel,
  });
};

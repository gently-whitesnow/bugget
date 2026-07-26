import { useState, useRef, useEffect, useMemo, useCallback } from "react";
import {
  Link as LinkIcon,
  ExternalLink,
  MoreHorizontal,
  Copy,
  Pencil,
  Trash2,
} from "lucide-react";
import { ReportLink } from "@/entities/report";
import { useFavicon } from "@/shared/lib";
import ActionDropdown, { ActionItem } from "@/shared/ui/ActionDropdown";

const hoverDelay = 500;

type Props = {
  link: ReportLink;
  onEdit: () => void;
  onDelete: () => void;
};

const LinkChip = ({ link, onEdit, onDelete }: Props) => {
  const [showPreview, setShowPreview] = useState(false);
  const [isHovering, setIsHovering] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const faviconUrl = useFavicon(link.link);
  const [displayFaviconUrl, setDisplayFaviconUrl] = useState<string | null>(
    null
  );

  const hoverTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setDisplayFaviconUrl(faviconUrl);
  }, [faviconUrl]);

  const clearHoverTimeout = () => {
    if (hoverTimeoutRef.current) {
      clearTimeout(hoverTimeoutRef.current);
      hoverTimeoutRef.current = null;
    }
  };

  const handleMouseEnter = () => {
    setIsHovering(true);
    hoverTimeoutRef.current = setTimeout(
      () => setShowPreview(true),
      hoverDelay
    );
  };

  const handleMouseLeave = () => {
    setIsHovering(false);
    if (!isMenuOpen) {
      setShowPreview(false);
    }
    clearHoverTimeout();
  };

  useEffect(() => clearHoverTimeout, []);

  const handleCopyLink = useCallback(async () => {
    await navigator.clipboard.writeText(link.link);
  }, [link.link]);

  const actionItems: ActionItem[] = useMemo(
    () => [
      {
        icon: <Copy className="w-4 h-4" />,
        label: "Копировать ссылку",
        onClick: handleCopyLink,
      },
      {
        icon: <Pencil className="w-4 h-4" />,
        label: "Редактировать",
        onClick: onEdit,
        className: "text-info",
      },
      {
        icon: <Trash2 className="w-4 h-4" />,
        label: "Удалить",
        onClick: onDelete,
        className: "text-error hover:bg-error/10",
      },
    ],
    [handleCopyLink, onEdit, onDelete]
  );

  return (
    <div
      className="relative"
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      <div className="flex max-w-full cursor-pointer items-center gap-1 rounded-lg bg-base-200 px-3 py-1.5 transition-colors hover:bg-base-300">
        {displayFaviconUrl ? (
          <img
            src={displayFaviconUrl}
            alt=""
            className="w-4 h-4"
            onError={() => setDisplayFaviconUrl(null)}
          />
        ) : (
          <LinkIcon className="w-4 h-4 text-base-content/70" />
        )}

        <a
          href={link.link}
          target="_blank"
          rel="noopener noreferrer"
          className="max-w-[min(40cqi,9.375rem)] truncate text-sm hover:underline"
        >
          {link.name}
        </a>

        {!isHovering && !isMenuOpen && (
          <ExternalLink className="w-4 h-4 text-base-content/50" />
        )}

        {(isHovering || isMenuOpen) && (
          <ActionDropdown
            items={actionItems}
            triggerIcon={
              <MoreHorizontal className="w-4 h-4 text-base-content/70" />
            }
            triggerClassName="hover:bg-base-content/10 rounded"
            menuPosition="start"
            onOpenChange={(open) => {
              setIsMenuOpen(open);
              if (open) setShowPreview(false);
            }}
          />
        )}
      </div>

      {showPreview && !isMenuOpen && (
        <div className="pointer-events-none absolute left-0 top-full z-20 mt-1 w-[min(100vw-2rem,18.75rem)] rounded-lg border border-base-300 bg-base-100 p-3 shadow-lg">
          <div className="font-medium text-sm mb-1 truncate">{link.name}</div>
          <div className="text-xs text-base-content/60 truncate">
            {link.link}
          </div>
        </div>
      )}
    </div>
  );
};

export default LinkChip;

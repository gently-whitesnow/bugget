import { useEffect, useRef } from "react";
import { ExternalLink, Trash2 } from "lucide-react";

type Props = {
  url: string;
  linkElement: HTMLElement;
  onClose: () => void;
  onDelete?: () => void;
};

const LinkPreview = ({ url, linkElement, onClose, onDelete }: Props) => {
  const tooltipRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const updatePosition = () => {
      if (!tooltipRef.current || !linkElement) return;

      const linkRect = linkElement.getBoundingClientRect();
      const tooltip = tooltipRef.current;

      // Временно показываем тултип для измерения размеров
      tooltip.style.visibility = "hidden";
      tooltip.style.display = "block";
      const tooltipRect = tooltip.getBoundingClientRect();
      tooltip.style.visibility = "visible";

      // Позиционируем тултип снизу от ссылки, по центру
      const top = linkRect.bottom + 8;
      const left = linkRect.left + linkRect.width / 2 - tooltipRect.width / 2;

      // Проверяем, не выходит ли тултип за границы экрана
      const viewportWidth = window.innerWidth;
      const viewportHeight = window.innerHeight;
      const padding = 8;

      let finalLeft = left;
      let finalTop = top;

      // Если тултип выходит за правую границу
      if (left + tooltipRect.width > viewportWidth - padding) {
        finalLeft = viewportWidth - tooltipRect.width - padding;
      }
      // Если тултип выходит за левую границу
      if (left < padding) {
        finalLeft = padding;
      }

      // Если тултип не помещается снизу, показываем сверху
      if (top + tooltipRect.height > viewportHeight - padding) {
        finalTop = linkRect.top - tooltipRect.height - 8;
      }

      tooltip.style.left = `${finalLeft}px`;
      tooltip.style.top = `${finalTop}px`;
    };

    // Используем requestAnimationFrame для измерения после рендера
    requestAnimationFrame(() => {
      updatePosition();
    });

    window.addEventListener("scroll", updatePosition, true);
    window.addEventListener("resize", updatePosition);

    return () => {
      window.removeEventListener("scroll", updatePosition, true);
      window.removeEventListener("resize", updatePosition);
    };
  }, [linkElement]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        tooltipRef.current &&
        !tooltipRef.current.contains(event.target as Node) &&
        !linkElement.contains(event.target as Node)
      ) {
        onClose();
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEscape);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [linkElement, onClose]);

  const handleOpenLink = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    window.open(url, "_blank", "noopener,noreferrer");
    onClose();
  };

  const handleDelete = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (onDelete) {
      onDelete();
    }
    onClose();
  };

  const preventFocusLoss = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
  };

  return (
    <div
      ref={tooltipRef}
      className="fixed z-50 bg-base-200 border border-base-300 rounded-lg shadow-lg pt-1 pb-1 pl-2 pr-2 pointer-events-auto"
      onClick={(e) => e.stopPropagation()}
    >
      <div className="flex items-center gap-2">
        <div className="flex-1 min-w-0">
          <div className="text-sm text-base-content break-all">{url}</div>
        </div>
        <div className="flex items-center gap-1 border-l border-base-300 pl-2">
          <button
            onClick={handleOpenLink}
            onMouseDown={preventFocusLoss}
            className="group p-1.5 hover:bg-base-300 rounded transition-colors cursor-pointer"
            title="Открыть ссылку"
            type="button"
          >
            <ExternalLink className="w-4 h-4 text-base-content/70 group-hover:text-base-content transition-colors" />
          </button>
          {onDelete && (
            <button
              onClick={handleDelete}
              onMouseDown={preventFocusLoss}
              className="group p-1.5 hover:bg-base-300 rounded transition-colors cursor-pointer"
              title="Удалить ссылку"
              type="button"
            >
              <Trash2 className="w-4 h-4 text-base-content/70 group-hover:text-base-content transition-colors" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default LinkPreview;

import { CSSProperties, useEffect, useState } from "react";
import { Bug, Plus } from "lucide-react";

type Props = {
  onClick: () => void;
  disabled: boolean;
  visible: boolean;
};

const FloatingAddBugButton = ({ onClick, disabled, visible }: Props) => {
  const [show, setShow] = useState(false);
  const [rightOffset, setRightOffset] = useState<string>(
    "var(--layout-page-padding-inline)"
  );

  useEffect(() => {
    if (visible) {
      setShow(true);
    } else {
      setShow(false);
    }
  }, [visible]);

  useEffect(() => {
    const updateOffset = () => {
      const mainElement = document.querySelector(".app-layout-main");

      if (!mainElement) {
        setRightOffset("var(--layout-page-padding-inline)");
        return;
      }

      const mainRect = mainElement.getBoundingClientRect();
      const inlinePadding = getComputedStyle(mainElement).paddingRight || "0px";
      setRightOffset(
        `calc(${Math.max(window.innerWidth - mainRect.right, 0)}px + ${inlinePadding})`
      );
    };

    updateOffset();

    const resizeObserver = new ResizeObserver(updateOffset);
    const mainElement = document.querySelector(".app-layout-main");

    if (mainElement) {
      resizeObserver.observe(mainElement);
    }

    window.addEventListener("resize", updateOffset);

    return () => {
      resizeObserver.disconnect();
      window.removeEventListener("resize", updateOffset);
    };
  }, []);

  const buttonStyle: CSSProperties = {
    right: rightOffset,
  };

  return (
    <button
      className={`btn btn-primary btn-circle fixed bottom-[clamp(1rem,4vw,2rem)] z-50 h-11 min-h-11 w-11 border-0 shadow-lg shadow-primary/20 transition-[background-color,box-shadow,opacity,transform] duration-150 ease-out hover:shadow-xl hover:shadow-primary/25 ${
        show
          ? "opacity-100 scale-100 translate-y-0"
          : "opacity-0 scale-75 translate-y-4 pointer-events-none"
      }`}
      style={buttonStyle}
      onClick={onClick}
      disabled={disabled}
      aria-label="Добавить баг"
      title="Добавить баг"
    >
      <span className="relative grid h-7 w-7 shrink-0 place-items-center rounded-full bg-primary-content/15">
        <Bug className="h-5 w-5" />
        <span className="absolute -right-1.5 -top-1.5 grid h-4 w-4 place-items-center rounded-full bg-primary-content text-primary shadow-sm">
          <Plus className="h-2.5 w-2.5" />
        </span>
      </span>
    </button>
  );
};

export default FloatingAddBugButton;

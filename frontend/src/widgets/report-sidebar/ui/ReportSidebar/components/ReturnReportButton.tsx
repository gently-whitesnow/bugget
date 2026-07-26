import { useEffect, useRef, useState } from "react";
import { useUnit } from "effector-react";
import {
  $lastResponsibleUserNameStore,
  $pastResponsibleUserIdStore,
  $responsibleUserIdStore,
  changeResponsibleUserIdEvent,
} from "@/entities/report";
import { $authUserStore } from "@/entities/user";

const animationDuration = 100; // ms

const formatName = (fullName?: string | null) =>
  fullName ? fullName.split(" ").slice(0, 2).join(" ") : "";

enum ButtonState {
  HIDDEN = "hidden",
  VISIBLE = "visible",
  CLICKED = "clicked",
  EXITING = "exiting",
}

const ReturnReportButton = () => {
  const responsibleUserId = useUnit($responsibleUserIdStore);
  const lastResponsibleUserName = useUnit($lastResponsibleUserNameStore);
  const pastResponsibleUserId = useUnit($pastResponsibleUserIdStore);
  const user = useUnit($authUserStore);
  const changeResponsibleUserId = useUnit(changeResponsibleUserIdEvent);

  const previousResponsibleLabel = formatName(lastResponsibleUserName);
  const canReturn =
    user.id === responsibleUserId &&
    !!pastResponsibleUserId &&
    user.id !== pastResponsibleUserId &&
    !!previousResponsibleLabel;

  const [state, setState] = useState<ButtonState>(ButtonState.HIDDEN);

  // Фиксируем текст, чтобы не дергался во время анимации
  const [label, setLabel] = useState(() => previousResponsibleLabel);
  const timeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    if (canReturn) {
      if (state !== ButtonState.VISIBLE) {
        setState(ButtonState.VISIBLE);
      }
      // Обновляем label только в состоянии visible (не после клика)
      if (state === ButtonState.VISIBLE) {
        setLabel(formatName(lastResponsibleUserName));
      }
    } else {
      if (state === ButtonState.VISIBLE) {
        // Кнопка видна, но не было клика → убираем без анимации
        setState(ButtonState.HIDDEN);
      } else if (state === ButtonState.CLICKED) {
        // Был клик → играем анимацию ухода
        setState(ButtonState.EXITING);
      }
    }
  }, [canReturn, state, lastResponsibleUserName]);

  // Таймаут для завершения анимации выхода
  useEffect(() => {
    if (state === ButtonState.EXITING) {
      timeoutRef.current = setTimeout(() => {
        setState(ButtonState.HIDDEN);
        timeoutRef.current = null;
      }, animationDuration);

      return () => {
        if (timeoutRef.current) {
          clearTimeout(timeoutRef.current);
          timeoutRef.current = null;
        }
      };
    }
  }, [state]);

  const handleClick = () => {
    if (!pastResponsibleUserId) return;
    setState(ButtonState.CLICKED);
    changeResponsibleUserId(pastResponsibleUserId);
  };

  if (state === ButtonState.HIDDEN) {
    return null;
  }

  return (
    <div
      className={[
        "transform transition-all ease-out duration-300",
        state === ButtonState.EXITING
          ? "opacity-0 translate-x-24 scale-95"
          : "opacity-100 translate-x-0 scale-100",
      ].join(" ")}
    >
      <div className="tooltip w-full" data-tip="Вернуть репорт">
        <button className="btn btn-primary w-full" onClick={handleClick}>
          <div className="flex items-center w-full justify-start gap-2">
            <span className="text-xl">&rarr;</span>
            <span className="text-sm font-normal">{label}</span>
          </div>
        </button>
      </div>
    </div>
  );
};

export default ReturnReportButton;

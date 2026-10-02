import type { RefObject } from "react";
import { X } from "lucide-react";
import { appHotkeys, formatHotkey, type Hotkey } from "@/shared/lib/keyboard";
import { markdownCommands } from "@/shared/lib/markdownEditing";

type Props = {
  ref: RefObject<HTMLDialogElement | null>;
};

type Row = { label: string; hotkeys: Hotkey[] };

const enter: Hotkey = { code: "Enter" };

const sections: { title: string; rows: Row[] }[] = [
  {
    title: "Навигация",
    rows: [
      {
        label: "Поиск",
        hotkeys: [appHotkeys.search, appHotkeys.searchSlash],
      },
      {
        label: "Горячие клавиши",
        hotkeys: [appHotkeys.help, appHotkeys.helpQuestion],
      },
    ],
  },
  {
    title: "Поля ввода",
    rows: [
      {
        label: "Отправить комментарий или шаг",
        hotkeys: [enter, { ...enter, mod: true }],
      },
      {
        label: "Сохранить результат бага",
        hotkeys: [{ ...enter, mod: true }],
      },
      {
        label: "Новая строка",
        hotkeys: [
          { ...enter, shift: true },
          { ...enter, alt: true },
        ],
      },
      { label: "Отменить правку", hotkeys: [{ code: "Escape" }] },
      {
        label: "Вложенность пункта списка",
        hotkeys: [{ code: "Tab" }, { code: "Tab", shift: true }],
      },
      { label: "Прикрепить файл", hotkeys: [appHotkeys.attachFile] },
    ],
  },
  {
    title: "Форматирование markdown",
    rows: markdownCommands.map(({ label, hotkeys }) => ({ label, hotkeys })),
  },
];

/** Открывается через ref.current.showModal(); закрывают Esc, кнопка и клик по фону. */
const HotkeysDialog = ({ ref }: Props) => {
  return (
    <dialog ref={ref} className="modal" aria-labelledby="hotkeys-dialog-title">
      <div className="modal-box relative max-w-lg">
        <form method="dialog">
          <button
            className="btn btn-sm btn-circle btn-ghost absolute right-2 top-2"
            aria-label="Закрыть"
          >
            <X className="h-4 w-4" />
          </button>
        </form>
        <h3 id="hotkeys-dialog-title" className="text-lg font-semibold">
          Горячие клавиши
        </h3>
        {sections.map(({ title, rows }) => (
          <section key={title} className="mt-4">
            <h4 className="text-xs font-medium tracking-wide text-base-content/60">
              {title}
            </h4>
            <dl className="mt-1 text-sm">
              {rows.map(({ label, hotkeys }) => (
                <div
                  key={label}
                  className="flex items-center justify-between gap-4 py-1"
                >
                  <dt>{label}</dt>
                  <dd className="flex shrink-0 items-center gap-1 text-base-content/60">
                    {hotkeys.map((hotkey, index) => (
                      <span key={index} className="flex items-center gap-1">
                        {index > 0 && "или"}
                        <kbd className="kbd">{formatHotkey(hotkey)}</kbd>
                      </span>
                    ))}
                  </dd>
                </div>
              ))}
            </dl>
          </section>
        ))}
      </div>
      <form method="dialog" className="modal-backdrop">
        <button tabIndex={-1} aria-hidden="true">
          Закрыть
        </button>
      </form>
    </dialog>
  );
};

export default HotkeysDialog;

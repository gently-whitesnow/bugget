import { EditorView } from "@codemirror/view";

const isSafe = (url: string) => /^https?:\/\//i.test(url);

/**
 * Ссылку открываем кликом, пока поле не в фокусе: в тексте её обычно читают, а не правят.
 * Когда поле уже правят, нужен ⌘ или Ctrl — иначе курсор некуда поставить.
 */
export const openLinkOnClick = EditorView.domEventHandlers({
  mousedown: (event, view) => {
    const withModifier = event.metaKey || event.ctrlKey;
    if (view.hasFocus && !withModifier) return false;

    const target = event.target as HTMLElement | null;
    const url = target?.closest("[data-md-url]")?.getAttribute("data-md-url");
    if (!url || !isSafe(url)) return false;

    event.preventDefault();
    // Обычная ссылка, а не window.open с параметрами окна: так браузер открывает вкладку,
    // а не попап, и ничего не блокирует.
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.target = "_blank";
    anchor.rel = "noopener noreferrer";
    anchor.click();
    return true;
  },
});

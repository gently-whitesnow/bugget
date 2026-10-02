// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";
import ResultTextarea from "./ResultTextarea";

const setup = (value: string, autoFocus = false) => {
  const onBlur = vi.fn();
  const onInput = vi.fn();
  const onPaste = vi.fn();
  const onEditingChange = vi.fn();
  const view = render(
    <ResultTextarea
      value={value}
      placeholder="Опишите результат..."
      autoFocus={autoFocus}
      onBlur={onBlur}
      onInput={onInput}
      onPaste={onPaste}
      onEditingChange={onEditingChange}
    />
  );
  const editor = () => view.container.querySelector(".cm-content")!;
  // Редактор приезжает отдельным чанком: ждём, пока он смонтируется.
  const settle = () => waitFor(() => expect(editor()).not.toBeNull());
  return {
    onBlur,
    onInput,
    onPaste,
    onEditingChange,
    editor,
    settle,
    container: view.container,
  };
};

describe("ResultTextarea", () => {
  afterEach(() => {
    cleanup();
  });

  it("показывает разметку без служебных символов", async () => {
    const { editor, settle } = setup("## Шаги\n**важно**");
    await settle();

    // Текст остаётся markdown, но решётки и звёздочки скрыты декорациями.
    expect(editor().textContent).toContain("Шаги");
    expect(editor().textContent).not.toContain("##");
    expect(editor().textContent).not.toContain("**");
  });

  it("рисует чекбокс у пункта задачи", async () => {
    const { container, settle } = setup("- [x] готово");
    await settle();

    const box = container.querySelector<HTMLInputElement>(
      "input[type=checkbox]"
    );
    expect(box?.checked).toBe(true);
    expect(box?.disabled).toBe(true);
  });

  it("отбивает заголовок от текста выше", async () => {
    const { container, settle } = setup("текст\n\n## Заголовок");
    await settle();

    expect(container.querySelectorAll(".cm-md-heading")).toHaveLength(1);
  });

  it("помечает ссылку, но прячет её адрес", async () => {
    const { container, settle } = setup("см. [документацию](https://ati.su)");
    await settle();

    const link = container.querySelector(".cm-md-link");
    expect(link?.textContent).toBe("документацию");
    expect(container.querySelector(".cm-content")?.textContent).not.toContain(
      "https://ati.su"
    );
  });

  it("открывает ссылку кликом, пока поле не правят", async () => {
    const opened: string[] = [];
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(function (this: HTMLAnchorElement) {
        opened.push(`${this.href}|${this.target}|${this.rel}`);
      });
    const { container, settle } = setup("см. [документацию](https://ati.su)");
    await settle();

    const link = container.querySelector(".cm-md-link")!;
    expect(link.getAttribute("data-md-url")).toBe("https://ati.su");

    fireEvent.mouseDown(link);

    expect(opened).toEqual(["https://ati.su/|_blank|noopener noreferrer"]);
    click.mockRestore();
  });

  it("отдаёт вставку владельцу: из буфера приходят файлы и cURL", async () => {
    const { editor, onPaste, settle } = setup("текст");
    await settle();

    fireEvent.paste(editor(), {
      clipboardData: { getData: () => "", files: [], items: [] },
    });

    expect(onPaste).toHaveBeenCalledTimes(1);
  });

  it("возвращает сохранённый текст по Esc", async () => {
    const { editor, onBlur, settle } = setup("сохранённый");
    await settle();

    fireEvent.focus(editor());
    fireEvent.input(editor(), { target: { textContent: "черновик" } });
    fireEvent.keyDown(editor(), { key: "Escape", code: "Escape" });
    fireEvent.blur(editor());

    expect(onBlur).toHaveBeenLastCalledWith("сохранённый");
  });

  it("сохраняет текст на потере фокуса", async () => {
    const { editor, onBlur, onEditingChange, settle } = setup("текст");
    await settle();

    fireEvent.focus(editor());
    fireEvent.blur(editor());

    expect(onBlur).toHaveBeenCalledWith("текст");
    expect(onEditingChange).toHaveBeenLastCalledWith(false);
  });

  it("сохраняет по ⌘Enter, а обычный Enter переносит строку", async () => {
    const { editor, onBlur, settle } = setup("текст");
    await settle();

    fireEvent.keyDown(editor(), { key: "Enter", code: "Enter" });
    expect(onBlur).not.toHaveBeenCalled();

    fireEvent.keyDown(editor(), { key: "Enter", code: "Enter", ctrlKey: true });
    expect(onBlur).toHaveBeenCalled();
  });
});

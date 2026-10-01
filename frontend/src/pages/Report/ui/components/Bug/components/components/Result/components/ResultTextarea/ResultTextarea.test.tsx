// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import ResultTextarea from "./ResultTextarea";

const setup = (value: string, autoFocus = false) => {
  const onBlur = vi.fn();
  const onInput = vi.fn();
  render(
    <ResultTextarea
      value={value}
      placeholder="Опишите результат..."
      autoFocus={autoFocus}
      onBlur={onBlur}
      onInput={onInput}
    />
  );
  return { onBlur, onInput };
};

const queryTextarea = () => screen.queryByRole("textbox");

describe("ResultTextarea", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders markdown when there is text and no focus", async () => {
    setup("**важно**");

    expect(queryTextarea()).toBeNull();
    expect((await screen.findByText("важно")).tagName).toBe("STRONG");
  });

  it("shows editor right away for empty value", () => {
    setup("");

    expect(queryTextarea()).not.toBeNull();
  });

  it("opens source on click and returns to view on blur", async () => {
    const { onBlur } = setup("**важно**");

    fireEvent.click(screen.getByLabelText("Опишите результат..."));
    const textarea = queryTextarea() as HTMLTextAreaElement;
    expect(textarea.value).toBe("**важно**");
    expect(document.activeElement).toBe(textarea);

    fireEvent.change(textarea, { target: { value: "_новое_" } });
    fireEvent.blur(textarea);

    expect(onBlur).toHaveBeenCalledWith("_новое_");
    expect(queryTextarea()).toBeNull();
    expect((await screen.findByText("новое")).tagName).toBe("EM");
  });

  it("breaks line on Enter and saves on Ctrl+Enter", () => {
    const { onBlur } = setup("");
    const textarea = queryTextarea() as HTMLTextAreaElement;
    fireEvent.focus(textarea);
    fireEvent.change(textarea, { target: { value: "строка" } });
    textarea.setSelectionRange(6, 6);

    fireEvent.keyDown(textarea, { key: "Enter", code: "Enter" });
    expect(textarea.value).toBe("строка\n");
    expect(onBlur).not.toHaveBeenCalled();

    fireEvent.keyDown(textarea, { key: "Enter", code: "Enter", ctrlKey: true });
    fireEvent.blur(textarea);
    expect(onBlur).toHaveBeenCalledWith("строка\n");
  });

  it("does not keep the height of erased text", () => {
    setup("**важно**");
    fireEvent.click(screen.getByLabelText("Опишите результат..."));
    const textarea = queryTextarea() as HTMLTextAreaElement;
    fireEvent.focus(textarea);
    expect(textarea.style.minHeight).not.toBe("");

    fireEvent.change(textarea, { target: { value: "" } });

    expect(textarea.style.minHeight).toBe("max(7.5rem, 0px)");
  });

  it("tells how to start editing", () => {
    setup("текст");

    const box = screen.getByLabelText("Опишите результат...");
    const hint = document.getElementById(box.getAttribute("aria-describedby")!);

    expect(hint?.textContent).toBe("Нажмите Enter, чтобы редактировать.");
  });

  it("opens source on Enter", () => {
    setup("text");

    fireEvent.keyDown(screen.getByLabelText("Опишите результат..."), {
      key: "Enter",
    });

    expect(queryTextarea()).not.toBeNull();
  });

  it("does not open editor when a link inside is clicked", async () => {
    setup("[docs](https://ati.su)");

    fireEvent.click(await screen.findByRole("link"));

    expect(queryTextarea()).toBeNull();
  });

  it("starts in editor when focused on mount", () => {
    setup("text", true);

    expect(queryTextarea()).not.toBeNull();
  });
});

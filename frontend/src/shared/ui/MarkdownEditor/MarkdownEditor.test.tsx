// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render } from "@testing-library/react";
import MarkdownEditor from "./MarkdownEditor";

const text = (container: HTMLElement) =>
  (container.querySelector(".cm-content") as HTMLElement).textContent;

const editor = (container: HTMLElement) =>
  container.querySelector(".cm-content") as HTMLElement;

describe("MarkdownEditor", () => {
  afterEach(() => {
    cleanup();
  });

  it("restores the saved text on Escape", () => {
    const onInput = vi.fn();
    const onCancel = vi.fn();
    const { container } = render(
      <MarkdownEditor value="saved" onInput={onInput} actions={{ onCancel }} />
    );
    const content = editor(container);

    fireEvent.keyDown(content, { key: "b", code: "KeyB", ctrlKey: true });
    expect(text(container)).not.toBe("saved");

    fireEvent.keyDown(content, { key: "Escape", code: "Escape" });

    // Что увидит onBlur после Esc: отменённая правка наружу не уходит.
    expect(text(container)).toBe("saved");
    expect(onInput).toHaveBeenLastCalledWith("saved");
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("does not let formatting exceed maxLength", () => {
    const { container } = render(
      <MarkdownEditor value={"a".repeat(64)} maxLength={64} />
    );

    fireEvent.keyDown(editor(container), {
      key: "b",
      code: "KeyB",
      ctrlKey: true,
    });

    expect(text(container)?.length).toBe(64);
  });
});

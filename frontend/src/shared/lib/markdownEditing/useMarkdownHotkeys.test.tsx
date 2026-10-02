// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { useMarkdownHotkeys } from "./useMarkdownHotkeys";

const Editor = ({
  initial,
  onSubmit,
}: {
  initial: string;
  onSubmit?: () => void;
}) => {
  const [value, setValue] = useState(initial);
  const handleKeyDown = useMarkdownHotkeys({ onSubmit });
  return (
    <textarea
      value={value}
      onChange={(event) => setValue(event.target.value)}
      onKeyDown={handleKeyDown}
    />
  );
};

const getTextarea = () => screen.getByRole("textbox") as HTMLTextAreaElement;

describe("useMarkdownHotkeys", () => {
  afterEach(() => {
    cleanup();
  });

  it("applies formatting through React state and keeps selection", () => {
    render(<Editor initial="make bold" />);
    const textarea = getTextarea();
    textarea.setSelectionRange(5, 9);

    fireEvent.keyDown(textarea, { key: "b", code: "KeyB", ctrlKey: true });

    expect(textarea.value).toBe("make **bold**");
    expect([textarea.selectionStart, textarea.selectionEnd]).toEqual([7, 11]);
  });

  it("submits on Enter without changing text", () => {
    const onSubmit = vi.fn();
    render(<Editor initial="text" onSubmit={onSubmit} />);
    const textarea = getTextarea();

    fireEvent.keyDown(textarea, { key: "Enter", code: "Enter" });

    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(textarea.value).toBe("text");
  });

  it("ignores keys during IME composition", () => {
    const onSubmit = vi.fn();
    render(<Editor initial="text" onSubmit={onSubmit} />);

    fireEvent.keyDown(getTextarea(), {
      key: "Enter",
      code: "Enter",
      isComposing: true,
    });

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("skips events already handled by consumer", () => {
    render(<Editor initial="x" />);
    const textarea = getTextarea();
    textarea.addEventListener("keydown", (event) => event.preventDefault(), {
      capture: true,
    });
    textarea.setSelectionRange(0, 1);

    fireEvent.keyDown(textarea, { key: "b", code: "KeyB", ctrlKey: true });

    expect(textarea.value).toBe("x");
  });
});

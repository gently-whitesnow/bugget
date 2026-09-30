// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import MarkdownTextarea from "./MarkdownTextarea";

const getTextarea = () => screen.getByRole("textbox") as HTMLTextAreaElement;

const pasteText = (textarea: HTMLTextAreaElement, text: string) =>
  fireEvent.paste(textarea, {
    clipboardData: { getData: () => text, files: [], items: [] },
  });

describe("MarkdownTextarea", () => {
  afterEach(() => {
    cleanup();
  });

  it("keeps typed text when owner does not update value", () => {
    const onInput = vi.fn();
    render(<MarkdownTextarea value="old" onInput={onInput} />);
    const textarea = getTextarea();

    fireEvent.change(textarea, { target: { value: "typed" } });

    expect(onInput).toHaveBeenCalledWith("typed");
    expect(textarea.value).toBe("typed");
  });

  it("applies value changed from outside", () => {
    const { rerender } = render(<MarkdownTextarea value="draft" />);
    rerender(<MarkdownTextarea value="" />);

    expect(getTextarea().value).toBe("");
  });

  it("formats selection and reports markdown", () => {
    const onInput = vi.fn();
    render(<MarkdownTextarea value="make bold" onInput={onInput} />);
    const textarea = getTextarea();
    textarea.setSelectionRange(5, 9);

    fireEvent.keyDown(textarea, { key: "b", code: "KeyB", ctrlKey: true });

    expect(onInput).toHaveBeenLastCalledWith("make **bold**");
  });

  it("submits current text on Enter", () => {
    const onSubmit = vi.fn();
    render(<MarkdownTextarea value="" onSubmit={onSubmit} />);
    const textarea = getTextarea();
    fireEvent.change(textarea, { target: { value: "text" } });

    fireEvent.keyDown(textarea, { key: "Enter", code: "Enter" });

    expect(onSubmit).toHaveBeenCalledWith("text");
    expect(textarea.value).toBe("text");
  });

  it("breaks line on Option+Enter even with submit", () => {
    const onSubmit = vi.fn();
    render(<MarkdownTextarea value="- a" onSubmit={onSubmit} />);
    const textarea = getTextarea();
    textarea.setSelectionRange(3, 3);

    fireEvent.keyDown(textarea, { key: "Enter", code: "Enter", altKey: true });

    expect(onSubmit).not.toHaveBeenCalled();
    expect(textarea.value).toBe("- a\n- ");
  });

  it("calls onCancel on Escape", () => {
    const onCancel = vi.fn();
    render(<MarkdownTextarea value="" onCancel={onCancel} />);

    fireEvent.keyDown(getTextarea(), { key: "Escape", code: "Escape" });

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("respects keys handled by owner", () => {
    const onSubmit = vi.fn();
    render(
      <MarkdownTextarea
        value=""
        onSubmit={onSubmit}
        onKeyDown={(event) => event.preventDefault()}
      />
    );

    fireEvent.keyDown(getTextarea(), { key: "Enter", code: "Enter" });

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("turns selection into link when url is pasted", () => {
    render(<MarkdownTextarea value="see docs" />);
    const textarea = getTextarea();
    textarea.setSelectionRange(4, 8);

    pasteText(textarea, "https://ati.su");

    expect(textarea.value).toBe("see [docs](https://ati.su)");
  });

  it("leaves paste to owner that handled it", () => {
    render(
      <MarkdownTextarea
        value="see docs"
        onPaste={(event) => event.preventDefault()}
      />
    );
    const textarea = getTextarea();
    textarea.setSelectionRange(4, 8);

    pasteText(textarea, "https://ati.su");

    expect(textarea.value).toBe("see docs");
  });

  it("opens file picker on ⌘U only when attachments are supported", () => {
    const onAttachFile = vi.fn();
    const { rerender } = render(<MarkdownTextarea value="" />);
    const key = { key: "u", code: "KeyU", ctrlKey: true };

    expect(fireEvent.keyDown(getTextarea(), key)).toBe(true);

    rerender(<MarkdownTextarea value="" onAttachFile={onAttachFile} />);
    expect(fireEvent.keyDown(getTextarea(), key)).toBe(false);
    expect(onAttachFile).toHaveBeenCalledTimes(1);
  });
});

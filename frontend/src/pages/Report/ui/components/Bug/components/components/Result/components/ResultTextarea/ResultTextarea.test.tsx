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

    fireEvent.click(
      screen.getByLabelText("Редактировать: Опишите результат...")
    );
    const textarea = queryTextarea() as HTMLTextAreaElement;
    expect(textarea.value).toBe("**важно**");
    expect(document.activeElement).toBe(textarea);

    fireEvent.change(textarea, { target: { value: "_новое_" } });
    fireEvent.blur(textarea);

    expect(onBlur).toHaveBeenCalledWith("_новое_");
    expect(queryTextarea()).toBeNull();
    expect((await screen.findByText("новое")).tagName).toBe("EM");
  });

  it("opens source on Enter", () => {
    setup("text");

    fireEvent.keyDown(
      screen.getByLabelText("Редактировать: Опишите результат..."),
      { key: "Enter" }
    );

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

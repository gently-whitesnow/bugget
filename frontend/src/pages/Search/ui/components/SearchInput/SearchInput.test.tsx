// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import SearchInput from "./SearchInput";

const getInput = () => screen.getByRole("searchbox") as HTMLInputElement;

describe("SearchInput", () => {
  afterEach(() => {
    cleanup();
  });

  it("is focused when search page opens", () => {
    render(<SearchInput />);

    expect(document.activeElement).toBe(getInput());
  });

  it("returns focus on Ctrl+K and slash", () => {
    render(<SearchInput />);
    const input = getInput();

    input.blur();
    fireEvent.keyDown(document.body, { key: "k", code: "KeyK", ctrlKey: true });
    expect(document.activeElement).toBe(input);

    input.blur();
    fireEvent.keyDown(document.body, { key: "/", code: "Slash" });
    expect(document.activeElement).toBe(input);
  });

  it("lets slash be typed inside the input", () => {
    render(<SearchInput />);

    expect(fireEvent.keyDown(getInput(), { key: "/", code: "Slash" })).toBe(
      true
    );
  });
});

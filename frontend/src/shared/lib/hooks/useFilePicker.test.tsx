// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, renderHook } from "@testing-library/react";
import { useFilePicker } from "./useFilePicker";

const findInput = () =>
  document.body.querySelector<HTMLInputElement>("input[type=file]");

describe("useFilePicker", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("passes every chosen file and removes the input", () => {
    const click = vi
      .spyOn(HTMLInputElement.prototype, "click")
      .mockImplementation(() => {});
    const onFile = vi.fn();
    const { result } = renderHook(() => useFilePicker(onFile));

    result.current();
    const input = findInput()!;
    expect(click).toHaveBeenCalledTimes(1);
    expect(input.multiple).toBe(true);

    const files = [new File(["a"], "a.txt"), new File(["b"], "b.png")];
    Object.defineProperty(input, "files", { value: files });
    input.dispatchEvent(new Event("change"));

    expect(onFile.mock.calls.map(([file]) => file.name)).toEqual([
      "a.txt",
      "b.png",
    ]);
    expect(findInput()).toBeNull();
  });

  it("removes the input when choice is cancelled", () => {
    vi.spyOn(HTMLInputElement.prototype, "click").mockImplementation(() => {});
    const { result } = renderHook(() => useFilePicker(vi.fn()));

    result.current();
    findInput()!.dispatchEvent(new Event("cancel"));

    expect(findInput()).toBeNull();
  });
});

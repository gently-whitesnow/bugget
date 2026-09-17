import { afterEach, describe, expect, it, vi } from "vitest";
import { isApplePlatform, matchesHotkey } from "./hotkey";

const event = (overrides: Partial<KeyboardEvent>) => ({
  key: "",
  code: "",
  metaKey: false,
  ctrlKey: false,
  shiftKey: false,
  altKey: false,
  ...overrides,
});

describe("matchesHotkey", () => {
  const bold = { code: "KeyB", mod: true };

  it("uses ⌘ on Apple and Ctrl elsewhere", () => {
    expect(matchesHotkey(event({ key: "b", metaKey: true }), bold, true)).toBe(
      true
    );
    expect(matchesHotkey(event({ key: "b", ctrlKey: true }), bold, false)).toBe(
      true
    );
    expect(matchesHotkey(event({ key: "b", ctrlKey: true }), bold, true)).toBe(
      false
    );
  });

  it("rejects extra modifiers", () => {
    expect(
      matchesHotkey(
        event({ key: "b", metaKey: true, ctrlKey: true }),
        bold,
        true
      )
    ).toBe(false);
    expect(
      matchesHotkey(
        event({ key: "B", metaKey: true, shiftKey: true }),
        bold,
        true
      )
    ).toBe(false);
  });

  it("falls back to physical key for non-Latin input", () => {
    expect(
      matchesHotkey(
        event({ key: "и", code: "KeyB", metaKey: true }),
        bold,
        true
      )
    ).toBe(true);
  });
});

describe("isApplePlatform", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("detects macOS and iPad", () => {
    vi.stubGlobal("navigator", { platform: "MacIntel" });
    expect(isApplePlatform()).toBe(true);
    vi.stubGlobal("navigator", { platform: "iPad" });
    expect(isApplePlatform()).toBe(true);
  });

  it("returns false for Windows", () => {
    vi.stubGlobal("navigator", { platform: "Win32" });
    expect(isApplePlatform()).toBe(false);
  });
});

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  ariaHotkey,
  formatHotkey,
  isApplePlatform,
  matchesHotkey,
} from "./hotkey";

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

describe("formatHotkey", () => {
  it("uses Apple symbols in HIG order", () => {
    expect(
      formatHotkey({ code: "KeyC", mod: true, alt: true, shift: true }, true)
    ).toBe("⌥⇧⌘C");
    expect(formatHotkey({ code: "Slash", shift: true }, true)).toBe("⇧/");
  });

  it("uses Ctrl labels elsewhere", () => {
    expect(formatHotkey({ code: "Digit1", mod: true, alt: true }, false)).toBe(
      "Ctrl+Alt+1"
    );
    expect(formatHotkey({ code: "Enter" }, false)).toBe("Enter");
  });
});

describe("ariaHotkey", () => {
  it("names modifiers for assistive technology", () => {
    expect(ariaHotkey({ code: "KeyK", mod: true }, true)).toBe("Meta+K");
    expect(ariaHotkey({ code: "KeyK", mod: true }, false)).toBe("Control+K");
  });
});

describe("matchesHotkey with slash", () => {
  const slash = { code: "Slash" };

  it("matches slash key on Cyrillic layout by physical key", () => {
    expect(matchesHotkey(event({ key: ".", code: "Slash" }), slash, true)).toBe(
      true
    );
    expect(
      matchesHotkey(
        event({ key: "?", code: "Slash", shiftKey: true }),
        slash,
        true
      )
    ).toBe(false);
  });
});

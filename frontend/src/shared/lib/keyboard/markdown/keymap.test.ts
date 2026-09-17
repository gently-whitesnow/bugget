import { describe, expect, it } from "vitest";
import { resolveMarkdownKey } from "./keymap";
import { render, snap } from "./testUtils";

type Key = {
  key: string;
  code?: string;
  metaKey?: boolean;
  ctrlKey?: boolean;
  shiftKey?: boolean;
  altKey?: boolean;
};

const press = (
  marked: string,
  key: Key,
  { canSubmit = false, isApple = true } = {}
) => {
  const snapshot = snap(marked);
  const action = resolveMarkdownKey(
    {
      code: key.code ?? "",
      metaKey: false,
      ctrlKey: false,
      shiftKey: false,
      altKey: false,
      ...key,
    },
    snapshot,
    { canSubmit, isApple }
  );
  if (action?.type === "edit") return render(snapshot, action.edit);
  return action?.type ?? null;
};

describe("resolveMarkdownKey", () => {
  describe("Enter", () => {
    it("submits on Enter and ⌘Enter when submit is available", () => {
      const options = { canSubmit: true };
      expect(press("a|", { key: "Enter" }, options)).toBe("submit");
      expect(press("a|", { key: "Enter", metaKey: true }, options)).toBe(
        "submit"
      );
    });

    it("breaks line on Shift+Enter and Option+Enter", () => {
      const options = { canSubmit: true };
      expect(press("a|", { key: "Enter", shiftKey: true }, options)).toBe(
        "a\n|"
      );
      expect(press("a|", { key: "Enter", altKey: true }, options)).toBe("a\n|");
    });

    it("continues list on line break", () => {
      expect(press("- a|", { key: "Enter", altKey: true })).toBe("- a\n- |");
    });

    it("breaks line on Enter without submit", () => {
      expect(press("- a|", { key: "Enter" })).toBe("- a\n- |");
      expect(press("a|", { key: "Enter", metaKey: true })).toBeNull();
    });

    it("uses Ctrl as modifier outside Apple platforms", () => {
      const options = { canSubmit: true, isApple: false };
      expect(press("a|", { key: "Enter", ctrlKey: true }, options)).toBe(
        "submit"
      );
      expect(press("a|", { key: "Enter", metaKey: true }, options)).toBeNull();
    });
  });

  describe("Tab", () => {
    it("indents list item", () => {
      expect(press("- a\n- b|", { key: "Tab" })).toBe("- a\n  - b|");
    });

    it("outdents list item on Shift+Tab", () => {
      expect(press("- a\n  - b|", { key: "Tab", shiftKey: true })).toBe(
        "- a\n- b|"
      );
    });

    it("keeps focus navigation outside of list without selection", () => {
      expect(press("plain|", { key: "Tab" })).toBeNull();
    });

    it("consumes Tab in list even when nothing changes", () => {
      expect(press("- a|", { key: "Tab" })).toBe("consume");
    });
  });

  describe("formatting", () => {
    it("formats by physical key on Cyrillic layout", () => {
      expect(press("{x}", { key: "и", code: "KeyB", metaKey: true })).toBe(
        "**{x}**"
      );
    });

    it("formats by typed Latin letter on remapped layout", () => {
      expect(press("{x}", { key: "b", code: "KeyN", metaKey: true })).toBe(
        "**{x}**"
      );
    });

    it("matches Option shortcuts despite altered key", () => {
      expect(
        press("a|", { key: "¡", code: "Digit1", metaKey: true, altKey: true })
      ).toBe("# a|");
    });

    it("requires exact modifiers", () => {
      expect(
        press("{x}", { key: "b", code: "KeyB", metaKey: true, shiftKey: true })
      ).toBeNull();
      expect(press("{x}", { key: "b", code: "KeyB" })).toBeNull();
    });

    it("consumes shortcut that has nothing to edit", () => {
      expect(press("a{  }", { key: "b", code: "KeyB", metaKey: true })).toBe(
        "consume"
      );
    });
  });
});

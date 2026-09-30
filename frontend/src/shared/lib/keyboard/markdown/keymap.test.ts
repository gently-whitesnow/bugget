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
  {
    canSubmit = false,
    submitOnEnter = true,
    canAttach = false,
    isApple = true,
  } = {}
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
    { canSubmit, submitOnEnter, canAttach, isApple }
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

    it("breaks line on Enter and submits on ⌘Enter when submitOnEnter is off", () => {
      const options = { canSubmit: true, submitOnEnter: false };
      expect(press("a|", { key: "Enter" }, options)).toBe("a\n|");
      expect(press("- x|", { key: "Enter" }, options)).toBe("- x\n- |");
      expect(press("a|", { key: "Enter", metaKey: true }, options)).toBe(
        "submit"
      );
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

    it("keeps focus navigation outside of list", () => {
      expect(press("plain|", { key: "Tab" })).toBeNull();
      expect(press("{plain\ntext}", { key: "Tab" })).toBeNull();
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

    it("accepts the second shortcut of a command", () => {
      const key = { key: "e", code: "KeyE", metaKey: true };
      expect(press("{x}", key)).toBe("`{x}`");
      expect(press("{x}", { ...key, shiftKey: true })).toBe("`{x}`");
    });

    it("consumes shortcut that has nothing to edit", () => {
      expect(press("a{  }", { key: "b", code: "KeyB", metaKey: true })).toBe(
        "consume"
      );
    });
  });

  describe("attach", () => {
    it("opens file picker on ⌘U when attachments are available", () => {
      const key = { key: "u", code: "KeyU", metaKey: true };
      expect(press("a|", key, { canAttach: true })).toBe("attach");
      expect(press("a|", key)).toBeNull();
    });
  });
});

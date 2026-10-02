import { describe, expect, it } from "vitest";
import { continueList, isInListItem, shiftIndent } from "./lists";
import { run, snap } from "./testUtils";

describe("continueList", () => {
  it.each([
    ["continues bullet list", "- one|", "- one\n- |"],
    [
      "increments ordered list number and keeps delimiter",
      "9) nine|",
      "9) nine\n10) |",
    ],
    ["keeps indentation of nested item", "- a\n  - b|", "- a\n  - b\n  - |"],
    [
      "continues task list with unchecked box",
      "- [x] done|",
      "- [x] done\n- [ ] |",
    ],
    ["continues quote", "> quoted|", "> quoted\n> |"],
    ["splits item when cursor is in the middle", "- ab|cd", "- ab\n- |cd"],
    ["replaces selection with new item", "- a{bc}", "- a\n- |"],
    ["exits list on empty item", "- a\n- |", "- a\n|"],
    [
      "continues a bullet list inside a quote",
      "> - пункт|",
      "> - пункт\n> - |",
    ],
    [
      "continues an ordered list inside a quote",
      "> 1. раз|",
      "> 1. раз\n> 2. |",
    ],
    ["returns null outside of list", "plain|", "<no edit>"],
    ["returns null when cursor is inside marker", "-| a", "<no edit>"],
  ])("%s", (_name, marked, expected) => {
    expect(run(marked, continueList)).toBe(expected);
  });
});

describe("isInListItem", () => {
  it("detects list line under cursor", () => {
    expect(isInListItem(snap("x\n1. a|"))).toBe(true);
    expect(isInListItem(snap("- a\nplain|"))).toBe(false);
  });
});

describe("shiftIndent", () => {
  const indent = (marked: string) => run(marked, (s) => shiftIndent(s, "in"));
  const outdent = (marked: string) => run(marked, (s) => shiftIndent(s, "out"));

  it("nests bullet item under previous sibling", () => {
    expect(indent("- a\n- b|")).toBe("- a\n  - b|");
  });

  it("nests under ordered item by its marker width and restarts numbering", () => {
    expect(indent("1. a\n2. b|")).toBe("1. a\n   1. b|");
  });

  it("keeps the task checkbox when nesting changes", () => {
    expect(indent("1. parent\n2. [x] done|")).toBe(
      "1. parent\n   1. [x] done|"
    );
    expect(indent("- parent\n- [ ] todo|")).toBe("- parent\n  - [ ] todo|");
    expect(outdent("1. a\n   1. [x] done|")).toBe("1. a\n2. [x] done|");
  });

  it("continues numbering of the level it returns to", () => {
    expect(outdent("1. a\n   1. b|")).toBe("1. a\n2. b|");
    expect(outdent("1. a\n2. b\n   1. c|")).toBe("1. a\n2. b\n3. c|");
  });

  it("keeps bullet markers as they are", () => {
    expect(indent("- a\n- b|")).toBe("- a\n  - b|");
  });

  it("does not nest deeper than one level", () => {
    expect(indent("- a\n  - b|")).toBe("<no edit>");
  });

  it("does not nest the first item", () => {
    expect(indent("- a|")).toBe("<no edit>");
  });

  it("nests under sibling skipping its children", () => {
    expect(indent("- a\n  - b\n- c|")).toBe("- a\n  - b\n  - c|");
  });

  it("outdents to parent level", () => {
    expect(outdent("1. a\n   - b|")).toBe("1. a\n- b|");
  });

  it("counts a tab as four columns and writes the indent back as spaces", () => {
    // Таб — четыре колонки, значит пункт уже вложен: Tab уровень не меняет.
    expect(indent("- a\n\t- b|")).toBe("- a\n    - b|");
    expect(outdent("- a\n\t- b|")).toBe("- a\n- b|");
  });

  it("indents selected plain lines by two spaces", () => {
    expect(indent("{a\n\nb}")).toBe("{  a\n\n  b}");
    expect(outdent("{   a\nb}")).toBe("{ a\nb}");
  });
});

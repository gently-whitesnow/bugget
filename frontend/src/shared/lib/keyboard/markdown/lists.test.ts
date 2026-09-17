import { describe, expect, it } from "vitest";
import { continueList, isInListItem, shiftIndent } from "./lists";
import { run, snap } from "./testUtils";

describe("continueList", () => {
  it("continues bullet list", () => {
    expect(run("- one|", continueList)).toBe("- one\n- |");
  });

  it("increments ordered list number and keeps delimiter", () => {
    expect(run("9) nine|", continueList)).toBe("9) nine\n10) |");
  });

  it("keeps indentation of nested item", () => {
    expect(run("- a\n  - b|", continueList)).toBe("- a\n  - b\n  - |");
  });

  it("continues task list with unchecked box", () => {
    expect(run("- [x] done|", continueList)).toBe("- [x] done\n- [ ] |");
  });

  it("continues quote", () => {
    expect(run("> quoted|", continueList)).toBe("> quoted\n> |");
  });

  it("splits item when cursor is in the middle", () => {
    expect(run("- ab|cd", continueList)).toBe("- ab\n- |cd");
  });

  it("replaces selection with new item", () => {
    expect(run("- a{bc}", continueList)).toBe("- a\n- |");
  });

  it("exits list on empty item", () => {
    expect(run("- a\n- |", continueList)).toBe("- a\n|");
  });

  it("returns null outside of list", () => {
    expect(run("plain|", continueList)).toBe("<no edit>");
  });

  it("returns null when cursor is inside marker", () => {
    expect(run("-| a", continueList)).toBe("<no edit>");
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

  it("nests under ordered item by its marker width", () => {
    expect(indent("1. a\n2. b|")).toBe("1. a\n   2. b|");
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

  it("indents selected plain lines by two spaces", () => {
    expect(indent("{a\n\nb}")).toBe("{  a\n\n  b}");
    expect(outdent("{   a\nb}")).toBe("{ a\nb}");
  });
});

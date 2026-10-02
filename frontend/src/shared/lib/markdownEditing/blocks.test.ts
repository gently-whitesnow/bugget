import { describe, expect, it } from "vitest";
import {
  toggleCodeBlock,
  toggleLineFormat,
  toggleQuote,
  type LineFormat,
} from "./blocks";
import { run } from "./testUtils";

const line = (marked: string, format: LineFormat) =>
  run(marked, (s) => toggleLineFormat(s, format));

const bullet: LineFormat = { kind: "bulletList" };
const ordered: LineFormat = { kind: "orderedList" };
const h2: LineFormat = { kind: "heading", level: 2 };

describe("toggleLineFormat", () => {
  it("adds heading and shifts cursor with text", () => {
    expect(line("Ti|tle", h2)).toBe("## Ti|tle");
  });

  it("removes heading of the same level", () => {
    expect(line("## Ti|tle", h2)).toBe("Ti|tle");
  });

  it("replaces heading of another level", () => {
    expect(line("# Ti|tle", h2)).toBe("## Ti|tle");
  });

  it("adds prefix to an empty line", () => {
    expect(line("|", bullet)).toBe("- |");
  });

  it("formats every non-empty selected line", () => {
    expect(line("{one\n\ntwo}", bullet)).toBe("{- one\n\n- two}");
  });

  it("numbers ordered list items sequentially", () => {
    expect(line("{a\nb\nc}", ordered)).toBe("{1. a\n2. b\n3. c}");
  });

  it("removes list when all lines already have it", () => {
    expect(line("{- a\n* b}", bullet)).toBe("{a\nb}");
  });

  it("converts ordered list to bullet list", () => {
    expect(line("{1. a\n2. b}", bullet)).toBe("{- a\n- b}");
  });

  it("does not take the line where selection ends at its start", () => {
    expect(line("{a\n}b", bullet)).toBe("{- a\n}b");
  });

  it("keeps indentation and quote before marker", () => {
    expect(line("  a|", bullet)).toBe("  - a|");
    expect(line("> a|", ordered)).toBe("> 1. a|");
  });

  it("puts cursor after new marker when it was inside old one", () => {
    expect(line("-| a", ordered)).toBe("1. |a");
  });
});

describe("toggleQuote", () => {
  it("quotes selected lines and keeps list markers", () => {
    expect(run("{- a\n- b}", toggleQuote)).toBe("{> - a\n> - b}");
  });

  it("removes quote when all lines are quoted", () => {
    expect(run("> a|", toggleQuote)).toBe("a|");
  });
});

describe("toggleCodeBlock", () => {
  it("wraps selected lines into fence", () => {
    expect(run("x\n{a\nb}\ny", toggleCodeBlock)).toBe("x\n```\n{a\nb}\n```\ny");
  });

  it("inserts empty fence on empty line", () => {
    expect(run("|", toggleCodeBlock)).toBe("```\n|\n```");
  });

  it("expands partial selection to whole lines", () => {
    expect(run("ab{c}d", toggleCodeBlock)).toBe("```\nab{c}d\n```");
  });

  it("removes surrounding fence", () => {
    expect(run("x\n```ts\n{a}\n```\ny", toggleCodeBlock)).toBe("x\n{a}\ny");
  });
});

import { describe, expect, it } from "vitest";
import { toggleInlineFormat, toggleLink, type InlineFormat } from "./inline";
import { run } from "./testUtils";

const format = (marked: string, kind: InlineFormat) =>
  run(marked, (s) => toggleInlineFormat(s, kind));

describe("toggleInlineFormat", () => {
  describe("with selection", () => {
    it("wraps selection and keeps it selected", () => {
      expect(format("a {bold} c", "bold")).toBe("a **{bold}** c");
      expect(format("a {gone} c", "strikethrough")).toBe("a ~~{gone}~~ c");
      expect(format("a {x()} c", "code")).toBe("a `{x()}` c");
    });

    it("unwraps when markers surround selection", () => {
      expect(format("a **{bold}** c", "bold")).toBe("a {bold} c");
      expect(format("a ~~{gone}~~ c", "strikethrough")).toBe("a {gone} c");
    });

    it("unwraps when markers are inside selection", () => {
      expect(format("a {**bold**} c", "bold")).toBe("a {bold} c");
      expect(format("a {_it_} c", "italic")).toBe("a {it} c");
    });

    it("trims whitespace captured by double click", () => {
      expect(format("a {word }c", "bold")).toBe("a **{word}** c");
    });

    it("ignores whitespace-only selection", () => {
      expect(format("a{   }c", "bold")).toBe("<no edit>");
    });

    it("uses underscore for italic between word boundaries", () => {
      expect(format("a {word} c", "italic")).toBe("a _{word}_ c");
    });

    it("uses asterisk for italic inside a word", () => {
      expect(format("при{вет}", "italic")).toBe("при*{вет}*");
    });

    it("does not treat bold markers as italic", () => {
      expect(format("**{x}**", "italic")).toBe("**_{x}_**");
    });

    it("unwraps single-asterisk italic and keeps bold of bold-italic", () => {
      expect(format("*{x}*", "italic")).toBe("{x}");
      expect(format("***{x}***", "bold")).toBe("*{x}*");
    });

    it("uses double backticks when code contains a backtick", () => {
      expect(format("{a`b}", "code")).toBe("``{a`b}``");
      expect(format("{`a}", "code")).toBe("`` {`a} ``");
    });
  });

  describe("with cursor", () => {
    it("inserts empty markers and places cursor between", () => {
      expect(format("a |", "bold")).toBe("a **|**");
    });

    it("removes empty markers on second press", () => {
      expect(format("a **|**", "bold")).toBe("a |");
      expect(format("a _|_", "italic")).toBe("a |");
    });

    it("wraps the word under cursor and keeps cursor position", () => {
      expect(format("say hel|lo now", "bold")).toBe("say **hel|lo** now");
    });

    it("unwraps the word under cursor", () => {
      expect(format("say **hel|lo** now", "bold")).toBe("say hel|lo now");
    });

    it("does not wrap the word when cursor is at its edge", () => {
      expect(format("hello| ", "bold")).toBe("hello**|** ");
    });

    it("uses asterisk for italic right after a word", () => {
      expect(format("hello|", "italic")).toBe("hello*|*");
    });
  });
});

describe("toggleLink", () => {
  it("wraps selected text and selects url placeholder", () => {
    expect(run("see {docs} here", toggleLink)).toBe("see [docs]({url}) here");
  });

  it("wraps selected url and puts cursor into text", () => {
    expect(run("{https://ati.su}", toggleLink)).toBe("[|](https://ati.su)");
  });

  it("inserts empty link at cursor", () => {
    expect(run("a |", toggleLink)).toBe("a [|]()");
  });

  it("unwraps an existing link around selection", () => {
    expect(run("[{docs}](https://ati.su) x", toggleLink)).toBe("{docs} x");
  });
});

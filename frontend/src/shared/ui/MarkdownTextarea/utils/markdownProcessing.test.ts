// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { getMarkdownLinksOnBlur } from "./markdownProcessing";

const makeDiv = (html: string): HTMLDivElement => {
  const div = document.createElement("div");
  // eslint-disable-next-line no-restricted-syntax
  div.innerHTML = html;
  return div;
};

describe("getMarkdownLinksOnBlur", () => {
  it("returns html representation of plain text", () => {
    const div = makeDiv("hello world");
    const result = getMarkdownLinksOnBlur(div);
    expect(result).toBe("hello world");
  });

  it("converts markdown links in text to anchor tags", () => {
    const div = makeDiv("see [Google](https://google.com) here");
    const result = getMarkdownLinksOnBlur(div);
    expect(result).toContain("<a ");
    expect(result).toContain("Google");
    expect(result).toContain("google.com");
  });

  it("preserves line breaks", () => {
    const div = makeDiv("line1<br>line2");
    const result = getMarkdownLinksOnBlur(div);
    expect(result).toContain("<br>");
  });

  it("escapes HTML entities in non-link text", () => {
    const div = makeDiv("a &amp; b");
    const result = getMarkdownLinksOnBlur(div);
    expect(result).toContain("&amp;");
  });

  it("preserves existing anchor elements through round-trip", () => {
    const div = makeDiv(
      '<a href="https://x.com" target="_blank" rel="noopener noreferrer" class="text-primary underline break-all hover:text-primary-focus cursor-pointer" data-link-url="https://x.com">Link</a>'
    );
    const result = getMarkdownLinksOnBlur(div);
    expect(result).toContain("<a ");
    expect(result).toContain("Link");
    expect(result).toContain("x.com");
  });
});

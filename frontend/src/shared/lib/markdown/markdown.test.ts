// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import {
  parseMarkdownLinks,
  isValidUrl,
  normalizeUrl,
  markdownToHtml,
  htmlToMarkdown,
} from "./markdown";

describe("parseMarkdownLinks", () => {
  it("returns empty array for plain text", () => {
    expect(parseMarkdownLinks("no links here")).toEqual([]);
  });

  it("returns empty array for empty string", () => {
    expect(parseMarkdownLinks("")).toEqual([]);
  });

  it("parses a single link", () => {
    const result = parseMarkdownLinks("see [Google](https://google.com) here");
    expect(result).toEqual([
      {
        text: "Google",
        url: "https://google.com",
        startIndex: 4,
        endIndex: 32,
        fullMatch: "[Google](https://google.com)",
      },
    ]);
  });

  it("parses multiple links", () => {
    const result = parseMarkdownLinks(
      "[a](http://a.com) and [b](http://b.com)"
    );
    expect(result).toHaveLength(2);
    expect(result[0].text).toBe("a");
    expect(result[0].url).toBe("http://a.com");
    expect(result[1].text).toBe("b");
    expect(result[1].url).toBe("http://b.com");
  });

  it("handles link with cyrillic text", () => {
    const result = parseMarkdownLinks("[Яндекс](https://ya.ru)");
    expect(result[0].text).toBe("Яндекс");
    expect(result[0].url).toBe("https://ya.ru");
  });

  it("ignores incomplete markdown syntax", () => {
    expect(parseMarkdownLinks("[text only]")).toEqual([]);
    expect(parseMarkdownLinks("(url only)")).toEqual([]);
    expect(parseMarkdownLinks("[text](")).toEqual([]);
  });
});

describe("isValidUrl", () => {
  it("accepts https URLs", () => {
    expect(isValidUrl("https://example.com")).toBe(true);
  });

  it("accepts http URLs", () => {
    expect(isValidUrl("http://example.com")).toBe(true);
  });

  it("rejects non-http protocols", () => {
    expect(isValidUrl("ftp://example.com")).toBe(false);
    expect(isValidUrl("javascript:alert(1)")).toBe(false);
  });

  it("rejects invalid URLs", () => {
    expect(isValidUrl("not a url")).toBe(false);
    expect(isValidUrl("")).toBe(false);
  });
});

describe("normalizeUrl", () => {
  it("preserves http:// URLs", () => {
    expect(normalizeUrl("http://example.com")).toBe("http://example.com");
  });

  it("preserves https:// URLs", () => {
    expect(normalizeUrl("https://example.com")).toBe("https://example.com");
  });

  it("adds https:// to bare domains", () => {
    expect(normalizeUrl("example.com")).toBe("https://example.com");
  });
});

describe("markdownToHtml", () => {
  it("returns empty string for empty input", () => {
    expect(markdownToHtml("")).toBe("");
  });

  it("converts plain text unchanged", () => {
    expect(markdownToHtml("hello world")).toBe("hello world");
  });

  it("escapes HTML entities", () => {
    expect(markdownToHtml("<script>alert(1)</script>")).toBe(
      "&lt;script&gt;alert(1)&lt;/script&gt;"
    );
  });

  it("escapes ampersands and quotes", () => {
    expect(markdownToHtml('A & B "C"')).toBe("A &amp; B &quot;C&quot;");
  });

  it("converts newlines to <br>", () => {
    expect(markdownToHtml("line1\nline2")).toBe("line1<br>line2");
  });

  it("converts markdown link to anchor tag", () => {
    const html = markdownToHtml("[Google](https://google.com)");
    expect(html).toContain("<a ");
    expect(html).toContain('href="https://google.com/"');
    expect(html).toContain(">Google</a>");
    expect(html).toContain('target="_blank"');
    expect(html).toContain('rel="noopener noreferrer"');
  });

  it("preserves text around links", () => {
    const html = markdownToHtml("see [link](https://a.com) here");
    expect(html).toMatch(/^see <a /);
    expect(html).toMatch(/ here$/);
  });

  it("renders invalid URL as plain text", () => {
    const html = markdownToHtml("[text](javascript:alert(1))");
    expect(html).not.toContain("<a");
    expect(html).toContain("text");
  });

  it("handles mixed content with newlines and links", () => {
    const html = markdownToHtml("before\n[link](https://x.com)\nafter");
    expect(html).toContain("<br>");
    expect(html).toContain("<a ");
  });
});

describe("htmlToMarkdown", () => {
  const parse = (html: string): string => {
    const div = document.createElement("div");
    // eslint-disable-next-line no-restricted-syntax
    div.innerHTML = html;
    return htmlToMarkdown(div);
  };

  it("extracts plain text", () => {
    expect(parse("hello world")).toBe("hello world");
  });

  it("converts <br> to newline", () => {
    expect(parse("line1<br>line2")).toBe("line1\nline2");
  });

  it("converts anchor to markdown link", () => {
    expect(parse('<a href="https://google.com">Google</a>')).toBe(
      "[Google](https://google.com)"
    );
  });

  it("handles mixed content", () => {
    const result = parse('text <a href="https://a.com">link</a> more');
    expect(result).toBe("text [link](https://a.com) more");
  });

  it("handles div blocks", () => {
    const result = parse("<div>line1</div><div>line2</div>");
    expect(result).toBe("line1\nline2");
  });

  it("handles empty element", () => {
    expect(parse("")).toBe("");
  });

  it("trims leading/trailing whitespace", () => {
    expect(parse("  hello  ")).toBe("hello");
  });
});

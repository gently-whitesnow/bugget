// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { validateAndNormalizeUrl, createLinkElement } from "./links";

describe("validateAndNormalizeUrl", () => {
  it("accepts https URL", () => {
    expect(validateAndNormalizeUrl("https://example.com")).toBe(
      "https://example.com"
    );
  });

  it("accepts http URL", () => {
    expect(validateAndNormalizeUrl("http://example.com")).toBe(
      "http://example.com"
    );
  });

  it("adds https to bare domain", () => {
    expect(validateAndNormalizeUrl("example.com")).toBe("https://example.com");
  });

  it("trims whitespace", () => {
    expect(validateAndNormalizeUrl("  https://example.com  ")).toBe(
      "https://example.com"
    );
  });

  it("returns null for plain text", () => {
    expect(validateAndNormalizeUrl("not a url")).toBeNull();
  });

  it("returns null for empty string", () => {
    expect(validateAndNormalizeUrl("")).toBeNull();
  });

  it("returns null for text with spaces and dots", () => {
    expect(validateAndNormalizeUrl("hello world.txt foo")).toBeNull();
  });

  it("accepts domain-like text without protocol", () => {
    expect(validateAndNormalizeUrl("google.com")).toBe("https://google.com");
  });

  it("accepts URL with path", () => {
    expect(validateAndNormalizeUrl("https://example.com/path?q=1")).toBe(
      "https://example.com/path?q=1"
    );
  });
});

describe("createLinkElement", () => {
  it("creates an anchor element with correct attributes", () => {
    const link = createLinkElement("https://example.com", "Example");

    expect(link.tagName).toBe("A");
    expect(link.href).toBe("https://example.com/");
    expect(link.target).toBe("_blank");
    expect(link.rel).toBe("noopener noreferrer");
    expect(link.textContent).toBe("Example");
    expect(link.getAttribute("data-link-url")).toBe("https://example.com");
  });

  it("has styling class", () => {
    const link = createLinkElement("https://a.com", "A");
    expect(link.className).toContain("text-primary");
    expect(link.className).toContain("underline");
  });
});

import { describe, expect, it } from "vitest";
import { normalizeUrl } from "./normalizeUrl";

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

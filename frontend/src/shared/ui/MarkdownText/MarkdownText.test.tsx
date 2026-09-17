// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import LazyMarkdownText from "./LazyMarkdownText";
import MarkdownText from "./MarkdownText";

const renderText = (text: string) =>
  render(<MarkdownText text={text} />).container.firstElementChild!;

describe("MarkdownText", () => {
  afterEach(() => {
    cleanup();
  });

  it("renders inline formatting", () => {
    const root = renderText("**bold** _it_ ~~gone~~ `code`");

    expect(root.querySelector("strong")?.textContent).toBe("bold");
    expect(root.querySelector("em")?.textContent).toBe("it");
    expect(root.querySelector("del")?.textContent).toBe("gone");
    expect(root.querySelector("code")?.textContent).toBe("code");
  });

  it("renders headings, lists and quotes", () => {
    const root = renderText("## Шаги\n1. открыть\n2. нажать\n\n> цитата");

    expect(root.querySelector("h2")?.textContent).toBe("Шаги");
    expect(root.querySelectorAll("ol > li")).toHaveLength(2);
    expect(root.querySelector("blockquote")?.textContent).toContain("цитата");
  });

  it("keeps single line breaks of plain text", () => {
    const root = renderText("line1\nline2");

    expect(root.querySelectorAll("br")).toHaveLength(1);
  });

  it("opens links in a new tab, including bare urls", () => {
    const root = renderText("[docs](https://ati.su) и https://example.com");
    const links = root.querySelectorAll("a");

    expect([...links].map((a) => a.getAttribute("href"))).toEqual([
      "https://ati.su",
      "https://example.com",
    ]);
    links.forEach((a) => {
      expect(a.target).toBe("_blank");
      expect(a.rel).toBe("noopener noreferrer");
    });
  });

  it("does not execute raw html", () => {
    const root = renderText('<img src=x onerror="alert(1)"><b>x</b>');

    expect(root.querySelector("img")).toBeNull();
    expect(root.querySelector("b")).toBeNull();
  });

  it("strips javascript urls", () => {
    const root = renderText("[click](javascript:alert(1))");

    expect(root.querySelector("a")?.getAttribute("href")).toBe("");
  });

  it("shows images as links instead of loading them", () => {
    const root = renderText("![скрин](https://ati.su/a.png)");

    expect(root.querySelector("img")).toBeNull();
    expect(root.querySelector("a")?.getAttribute("href")).toBe(
      "https://ati.su/a.png"
    );
  });

  it("renders task list checkboxes as read-only", () => {
    const root = renderText("- [x] done\n- [ ] todo");
    const boxes = root.querySelectorAll<HTMLInputElement>("input");

    expect([...boxes].map((box) => [box.checked, box.disabled])).toEqual([
      [true, true],
      [false, true],
    ]);
  });
});

describe("LazyMarkdownText", () => {
  afterEach(() => {
    cleanup();
  });

  it("shows plain text until markdown chunk is loaded", async () => {
    const { container } = render(<LazyMarkdownText text="**bold**" />);

    expect(container.textContent).toBe("**bold**");
    expect((await screen.findByText("bold")).tagName).toBe("STRONG");
  });
});

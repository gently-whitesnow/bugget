// @vitest-environment jsdom
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { insertPlainTextAtRange } from "./insertPlaintextAtRange";

const setupEditable = (text: string) => {
  const div = document.createElement("div");
  div.contentEditable = "true";
  div.textContent = text;
  document.body.appendChild(div);
  return div;
};

const makeCollapsedRange = (node: Node, offset: number) => {
  const range = document.createRange();
  range.setStart(node, offset);
  range.setEnd(node, offset);

  const selection = window.getSelection()!;
  selection.removeAllRanges();
  selection.addRange(range);

  return { range, selection };
};

const makeSelectionRange = (
  node: Node,
  startOffset: number,
  endOffset: number
) => {
  const range = document.createRange();
  range.setStart(node, startOffset);
  range.setEnd(node, endOffset);

  const selection = window.getSelection()!;
  selection.removeAllRanges();
  selection.addRange(range);

  return { range, selection };
};

describe("insertPlainTextAtRange", () => {
  let container: HTMLDivElement;
  let execCommandMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    execCommandMock = vi.fn(() => false);
    // @ts-expect-error - execCommand is deprecated but still supported
    document.execCommand = execCommandMock;
  });

  afterEach(() => {
    container?.remove();
    vi.restoreAllMocks();
  });

  describe("when execCommand is unavailable (fallback path)", () => {
    beforeEach(() => {
      execCommandMock.mockReturnValue(false);
    });

    it("inserts text at cursor position", () => {
      container = setupEditable("helloworld");
      const { range, selection } = makeCollapsedRange(container.firstChild!, 5);

      insertPlainTextAtRange({
        range,
        selection,
        clipboardText: " ",
      });

      expect(container.textContent).toBe("hello world");
    });

    it("replaces selected text", () => {
      container = setupEditable("hello world");
      const { range, selection } = makeSelectionRange(
        container.firstChild!,
        6,
        11
      );

      insertPlainTextAtRange({
        range,
        selection,
        clipboardText: "universe",
      });

      expect(container.textContent).toBe("hello universe");
    });

    it("inserts at the beginning", () => {
      container = setupEditable("world");
      const { range, selection } = makeCollapsedRange(container.firstChild!, 0);

      insertPlainTextAtRange({
        range,
        selection,
        clipboardText: "hello ",
      });

      expect(container.textContent).toBe("hello world");
    });

    it("inserts at the end", () => {
      container = setupEditable("hello");
      const { range, selection } = makeCollapsedRange(container.firstChild!, 5);

      insertPlainTextAtRange({
        range,
        selection,
        clipboardText: " world",
      });

      expect(container.textContent).toBe("hello world");
    });
  });

  describe("when execCommand succeeds", () => {
    beforeEach(() => {
      execCommandMock.mockReturnValue(true);
    });

    it("delegates to execCommand and does not use Range fallback", () => {
      container = setupEditable("hello");
      const { range, selection } = makeCollapsedRange(container.firstChild!, 5);
      const insertNodeSpy = vi.spyOn(range, "insertNode");

      insertPlainTextAtRange({
        range,
        selection,
        clipboardText: " world",
      });

      expect(document.execCommand).toHaveBeenCalledWith(
        "insertText",
        false,
        " world"
      );
      expect(insertNodeSpy).not.toHaveBeenCalled();
    });
  });
});

// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { INDENT, indentFragmentLines, outdentFragmentLines } from "./indent";

const textFragment = (...parts: (string | "br")[]) => {
  const frag = document.createDocumentFragment();
  for (const part of parts) {
    if (part === "br") {
      frag.appendChild(document.createElement("br"));
    } else {
      frag.appendChild(document.createTextNode(part));
    }
  }
  return frag;
};

const fragmentToText = (frag: DocumentFragment): string => {
  let result = "";
  for (const node of Array.from(frag.childNodes)) {
    if (node.nodeType === Node.TEXT_NODE) {
      result += node.textContent;
    } else if ((node as Element).tagName === "BR") {
      result += "\n";
    }
  }
  return result;
};

describe("INDENT", () => {
  it("is two spaces", () => {
    expect(INDENT).toBe("  ");
  });
});

describe("indentFragmentLines", () => {
  it("adds indent to single-line text", () => {
    const frag = textFragment("hello");
    indentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("  hello");
  });

  it("adds indent after each <br>", () => {
    const frag = textFragment("line1", "br", "line2", "br", "line3");
    indentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("  line1\n  line2\n  line3");
  });

  it("works with custom indent", () => {
    const frag = textFragment("a", "br", "b");
    indentFragmentLines(frag, "    ");
    expect(fragmentToText(frag)).toBe("    a\n    b");
  });

  it("handles empty fragment", () => {
    const frag = document.createDocumentFragment();
    indentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("  ");
  });
});

describe("outdentFragmentLines", () => {
  it("removes indent from single-line text", () => {
    const frag = textFragment("  hello");
    outdentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("hello");
  });

  it("removes indent after each <br>", () => {
    const frag = textFragment("  line1", "br", "  line2");
    outdentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("line1\nline2");
  });

  it("removes only up to indent-length spaces", () => {
    const frag = textFragment("    deep");
    outdentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("  deep");
  });

  it("does nothing if no leading spaces", () => {
    const frag = textFragment("no indent");
    outdentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("no indent");
  });

  it("removes text node entirely if it becomes empty", () => {
    const frag = textFragment("  ", "br", "  ");
    outdentFragmentLines(frag);
    const textNodes = Array.from(frag.childNodes).filter(
      (n) => n.nodeType === Node.TEXT_NODE
    );
    expect(textNodes).toHaveLength(0);
  });

  it("indent then outdent is identity", () => {
    const frag = textFragment("line1", "br", "line2");
    indentFragmentLines(frag);
    outdentFragmentLines(frag);
    expect(fragmentToText(frag)).toBe("line1\nline2");
  });
});

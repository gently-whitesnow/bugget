import { HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import { EditorView } from "@codemirror/view";
import { tags } from "@lezer/highlight";

/** Одна поверхность вместо «исходник или вид»: разметка читается прямо в тексте. */
const markdownHighlight = HighlightStyle.define([
  { tag: tags.heading1, fontSize: "1.125rem", fontWeight: "600" },
  { tag: tags.heading2, fontSize: "1rem", fontWeight: "600" },
  {
    tag: [tags.heading3, tags.heading4, tags.heading5, tags.heading6],
    fontWeight: "600",
  },
  { tag: tags.strong, fontWeight: "600" },
  { tag: tags.emphasis, fontStyle: "italic" },
  { tag: tags.strikethrough, textDecoration: "line-through" },
  {
    tag: tags.link,
    color: "var(--color-info)",
    textDecoration: "underline",
  },
  { tag: tags.url, color: "var(--color-info)" },
  {
    tag: [tags.monospace, tags.contentSeparator],
    background: "var(--color-base-200)",
    borderRadius: "0.25rem",
  },
  {
    tag: tags.quote,
    color: "color-mix(in oklab, var(--color-base-content) 72%, transparent)",
  },
  {
    tag: [tags.processingInstruction, tags.meta],
    color: "color-mix(in oklab, var(--color-base-content) 45%, transparent)",
  },
]);

const fieldLook = EditorView.theme({
  // Высота на всю коробку: иначе клик ниже последней строки не ставит фокус.
  "&": {
    height: "100%",
    backgroundColor: "transparent",
    color: "inherit",
    fontSize: "inherit",
    fontFamily: "inherit",
  },
  "&.cm-focused": { outline: "none" },
  ".cm-content": {
    padding: "0",
    minHeight: "100%",
    fontFamily: "inherit",
    caretColor: "var(--color-base-content)",
  },
  ".cm-line": { padding: "0" },
  // Воздух между блоками, как в отрисовке.
  ".cm-line.cm-md-heading": { marginTop: "0.75em" },
  ".cm-line.cm-md-heading:first-child": { marginTop: "0" },
  ".cm-line.cm-md-quote": {
    borderLeft:
      "2px solid color-mix(in oklab, var(--color-base-content) 20%, transparent)",
    paddingLeft: "0.75em",
    color: "color-mix(in oklab, var(--color-base-content) 72%, transparent)",
  },
  ".cm-content .cm-md-link": { cursor: "pointer" },
  ".cm-scroller": {
    fontFamily: "inherit",
    lineHeight: "1.25rem",
    alignItems: "stretch",
  },
  ".cm-placeholder": {
    color: "color-mix(in oklab, var(--color-base-content) 40%, transparent)",
  },
});

export const markdownLook = [syntaxHighlighting(markdownHighlight), fieldLook];

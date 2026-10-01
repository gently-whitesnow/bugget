import { syntaxTree } from "@codemirror/language";
import {
  Decoration,
  ViewPlugin,
  WidgetType,
  type DecorationSet,
  type EditorView,
  type ViewUpdate,
} from "@codemirror/view";

/** Маркеры списка сюда не входят: без них строка теряет смысл. */
const markerNodes = new Set([
  "HeaderMark",
  "EmphasisMark",
  "StrikethroughMark",
  "CodeMark",
  "QuoteMark",
  "LinkMark",
  "URL",
]);

const lineClasses: Record<string, string> = {
  ATXHeading1: "cm-md-heading",
  ATXHeading2: "cm-md-heading",
  ATXHeading3: "cm-md-heading",
  ATXHeading4: "cm-md-heading",
  ATXHeading5: "cm-md-heading",
  ATXHeading6: "cm-md-heading",
  Blockquote: "cm-md-quote",
};

class CheckboxWidget extends WidgetType {
  constructor(private readonly checked: boolean) {
    super();
  }

  eq(other: CheckboxWidget) {
    return other.checked === this.checked;
  }

  toDOM() {
    const box = document.createElement("input");
    box.type = "checkbox";
    box.checked = this.checked;
    box.disabled = true;
    box.className = "checkbox checkbox-xs align-text-bottom";
    return box;
  }
}

const hidden = Decoration.replace({});

/** Адрес кладём в атрибут: клик ловится по самому элементу, без пересчёта координат. */
const linkMark = (url: string) =>
  Decoration.mark({ class: "cm-md-link", attributes: { "data-md-url": url } });

const build = (view: EditorView): DecorationSet => {
  const ranges: { from: number; to: number; value: Decoration }[] = [];
  const builder = {
    add: (from: number, to: number, value: Decoration) =>
      ranges.push({ from, to, value }),
  };
  // Строка с курсором показывает исходник: иначе правку не навести вслепую.
  const openLines = view.hasFocus
    ? new Set(
        view.state.selection.ranges.map(
          (range) => view.state.doc.lineAt(range.head).number
        )
      )
    : new Set<number>();

  for (const { from, to } of view.visibleRanges) {
    syntaxTree(view.state).iterate({
      from,
      to,
      enter: (node) => {
        const lineClass = lineClasses[node.name];
        if (lineClass) {
          const line = view.state.doc.lineAt(node.from);
          builder.add(
            line.from,
            line.from,
            Decoration.line({ class: lineClass })
          );
          return;
        }

        if (node.name === "Link") {
          const target = node.node.getChild("URL");
          const url = target
            ? view.state.doc.sliceString(target.from, target.to)
            : null;
          if (url) builder.add(node.from, node.to, linkMark(url));
          return;
        }

        // Адрес внутри [текста](url) прячется ниже как служебный символ.
        if (node.name === "URL" && node.node.parent?.name !== "Link") {
          const url = view.state.doc.sliceString(node.from, node.to);
          builder.add(node.from, node.to, linkMark(url));
          return;
        }

        if (node.name === "TaskMarker") {
          const mark = view.state.doc.sliceString(node.from, node.to);
          builder.add(
            node.from,
            node.to,
            Decoration.replace({
              widget: new CheckboxWidget(mark.toLowerCase().includes("x")),
            })
          );
          return;
        }

        if (!markerNodes.has(node.name) || node.from === node.to) return;
        if (openLines.has(view.state.doc.lineAt(node.from).number)) return;
        builder.add(node.from, node.to, hidden);
      },
    });
  }

  // Диапазоны перекрываются, поэтому сортирует набор, а не строитель.
  return Decoration.set(
    ranges.map(({ from, to, value }) => value.range(from, to)),
    true
  );
};

/** Live preview: разметка читается как текст, а исходник открывается там, где правят. */
export const markdownDecorations = ViewPlugin.fromClass(
  class {
    decorations: DecorationSet;

    constructor(view: EditorView) {
      this.decorations = build(view);
    }

    update(update: ViewUpdate) {
      if (
        update.docChanged ||
        update.selectionSet ||
        update.viewportChanged ||
        update.focusChanged
      ) {
        this.decorations = build(update.view);
      }
    }
  },
  { decorations: (plugin) => plugin.decorations }
);

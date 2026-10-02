import {
  defineLanguageFacet,
  Language,
  languageDataProp,
} from "@codemirror/language";
import { GFM, parser as markdownParser } from "@lezer/markdown";

/**
 * Язык собираем сами, а не берём @codemirror/lang-markdown: тот ради подсветки кода
 * внутри блоков тянет парсеры HTML, CSS и JavaScript — это +170 КБ gzip в бандл.
 */
const markdownFacet = defineLanguageFacet({
  commentTokens: { block: { open: "<!--", close: "-->" } },
});

const parser = markdownParser.configure([
  // GFM: зачёркивание, таблицы и чекбоксы задач — тот же набор, что в отрисовке.
  GFM,
  { props: [languageDataProp.add({ Document: markdownFacet })] },
]);

export const markdownLanguage = new Language(
  markdownFacet,
  parser,
  [],
  "markdown"
);

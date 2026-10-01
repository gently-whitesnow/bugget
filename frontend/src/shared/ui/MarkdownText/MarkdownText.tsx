import { memo } from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import remarkBreaks from "remark-breaks";
import remarkGfm from "remark-gfm";
import "./MarkdownText.css";

type Props = {
  text: string;
  className?: string;
};

const linkClassName =
  "break-all text-info underline underline-offset-2 transition-colors hover:text-primary";

// remark-breaks: одиночный перенос строки остаётся переносом, как в старых текстах.
const remarkPlugins = [remarkGfm, remarkBreaks];

const components: Components = {
  a: ({ href, children }) => (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className={linkClassName}
    >
      {children}
    </a>
  ),
  // Внешние картинки не грузим: адрес из текста репорта показываем ссылкой.
  img: ({ src, alt }) =>
    typeof src === "string" ? (
      <a
        href={src}
        target="_blank"
        rel="noopener noreferrer"
        className={linkClassName}
      >
        {alt || src}
      </a>
    ) : null,
  input: ({ type, checked }) =>
    type === "checkbox" ? (
      <input
        type="checkbox"
        checked={checked}
        disabled
        className="checkbox checkbox-xs"
      />
    ) : null,
  table: ({ children }) => (
    <div className="overflow-x-auto">
      <table>{children}</table>
    </div>
  ),
};

/** Отрисовка markdown из полей репорта. Сырой HTML не исполняется, опасные URL вырезаются. */
const MarkdownText = ({ text, className = "" }: Props) => (
  <div className={`markdown-text ${className}`.trim()}>
    <ReactMarkdown remarkPlugins={remarkPlugins} components={components}>
      {text}
    </ReactMarkdown>
  </div>
);

// Разбор markdown чистый по тексту, а перерисовки родителя частые: без memo он
// пересчитывается на каждое нажатие в соседнем поле.
export default memo(MarkdownText);

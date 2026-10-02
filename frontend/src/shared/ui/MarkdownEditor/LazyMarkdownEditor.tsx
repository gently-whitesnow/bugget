import { lazy, Suspense, type ComponentProps } from "react";

// CodeMirror — около 97 КБ gzip: грузим его только там, где правят текст.
const MarkdownEditor = lazy(() => import("./MarkdownEditor"));

type Props = ComponentProps<typeof MarkdownEditor>;

const LazyMarkdownEditor = ({ value, className = "", ...props }: Props) => (
  <Suspense
    fallback={
      // Пока чанк грузится, текст виден как есть: без пустого места и прыжка вёрстки.
      <div className={`whitespace-pre-wrap ${className}`.trim()}>{value}</div>
    }
  >
    <MarkdownEditor value={value} className={className} {...props} />
  </Suspense>
);

export default LazyMarkdownEditor;

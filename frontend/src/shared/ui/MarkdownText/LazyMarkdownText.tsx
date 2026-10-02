import { lazy, memo, Suspense, type ComponentProps } from "react";

// react-markdown с плагинами — около 37 KB gzip: грузим, только когда текст показывают.
const MarkdownText = lazy(() => import("./MarkdownText"));

type Props = ComponentProps<typeof MarkdownText>;

const LazyMarkdownText = ({ text, className = "" }: Props) => (
  <Suspense
    fallback={
      // Пока чанк грузится, текст виден как есть: без пустого места и прыжка вёрстки.
      <div className={`whitespace-pre-wrap ${className}`.trim()}>{text}</div>
    }
  >
    <MarkdownText text={text} className={className} />
  </Suspense>
);

export default memo(LazyMarkdownText);

import { lazy, Suspense, type ComponentProps } from "react";

// react-markdown с плагинами — около 37 KB gzip: грузим, только когда текст показывают.
const MarkdownText = lazy(() => import("./MarkdownText"));

type Props = ComponentProps<typeof MarkdownText>;

const LazyMarkdownText = ({ text, className = "", ...props }: Props) => (
  <Suspense
    fallback={
      // Пока чанк грузится, текст виден как есть: без пустого места и прыжка вёрстки.
      <div className={`whitespace-pre-wrap ${className}`.trim()} {...props}>
        {text}
      </div>
    }
  >
    <MarkdownText text={text} className={className} {...props} />
  </Suspense>
);

export default LazyMarkdownText;

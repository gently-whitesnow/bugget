import { useMemo, HTMLAttributes } from "react";
import { parseMarkdownLinks } from "@/shared/lib/markdown";
import MarkdownLinks from "./components/MarkdownLinks";
import PlainTextLinks from "./components/PlainTextLinks";

type Props = {
  text: string;
} & HTMLAttributes<HTMLDivElement>;

const AutoLinkText = ({ text, ...props }: Props) => {
  const linkifiedContent = useMemo(() => {
    if (!text) {
      return null;
    }

    const markdownLinks = parseMarkdownLinks(text);

    if (markdownLinks.length > 0) {
      return <MarkdownLinks text={text} />;
    }

    return <PlainTextLinks text={text} />;
  }, [text]);

  return <div {...props}>{linkifiedContent}</div>;
};

export default AutoLinkText;

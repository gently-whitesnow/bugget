import { markdownToHtml } from "@/shared/lib/markdown";

type Props = {
  text: string;
};

const MarkdownLinks = ({ text }: Props) => {
  const html = markdownToHtml(text);
  return <span dangerouslySetInnerHTML={{ __html: html }} />;
};

export default MarkdownLinks;

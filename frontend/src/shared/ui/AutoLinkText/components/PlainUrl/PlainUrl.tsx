import { normalizeUrl } from "@/shared/lib/markdown";

type Props = {
  url: string;
};

const linkClassName =
  "break-all text-info underline underline-offset-2 transition-colors hover:text-primary";

const PlainUrl = ({ url }: Props) => {
  const href = normalizeUrl(url);
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className={linkClassName}
      onClick={(e) => e.stopPropagation()}
    >
      {url}
    </a>
  );
};

export default PlainUrl;

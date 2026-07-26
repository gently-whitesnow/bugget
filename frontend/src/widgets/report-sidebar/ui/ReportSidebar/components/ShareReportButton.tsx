import { useState } from "react";
import { useParams } from "react-router";
import { copyToClipboard } from "@/shared/lib/clipboard";

const ShareReportButton = () => {
  const { reportId } = useParams();
  const [isCopied, setIsCopied] = useState(false);

  const handleShareReport = async () => {
    if (!reportId) return;

    try {
      const currentUrl = window.location.href;
      await copyToClipboard(currentUrl);
      setIsCopied(true);
      setTimeout(() => setIsCopied(false), 2000);
    } catch (error) {
      console.error("Failed to copy link:", error);
    }
  };

  if (!reportId) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2">
      <button
        onClick={handleShareReport}
        className="btn btn-primary font-normal"
      >
        {isCopied ? "Скопировано!" : "Поделиться репортом"}
      </button>
    </div>
  );
};

export default ShareReportButton;

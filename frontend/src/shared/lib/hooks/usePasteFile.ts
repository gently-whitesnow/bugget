import { useCallback } from "react";

import {
  createCurlAttachmentFile,
  getClipboardFiles,
} from "@/shared/lib/clipboard";

type UsePasteFileOptions = {
  onFileUpload: (file: File) => void | Promise<unknown>;
};

export const usePasteFile = ({ onFileUpload }: UsePasteFileOptions) => {
  const handlePaste = useCallback(
    (event: React.ClipboardEvent<HTMLDivElement>) => {
      const files = getClipboardFiles(event.clipboardData);
      const clipboardText = event.clipboardData?.getData("text/plain") ?? "";
      const curlFile = createCurlAttachmentFile(clipboardText);
      const uploadFiles = curlFile ? [...files, curlFile] : files;
      if (uploadFiles.length === 0) return;

      event.preventDefault();

      uploadFiles.forEach((file) => {
        Promise.resolve(onFileUpload(file)).catch((err) => {
          console.error("Ошибка при загрузке файла:", err);
        });
      });
    },
    [onFileUpload]
  );

  return { handlePaste };
};

export default usePasteFile;

import { useCallback, useRef } from "react";

/** Открывает системный выбор файлов без своей разметки — для горячей клавиши ⌘U. */
export const useFilePicker = (onFile: (file: File) => unknown) => {
  const onFileRef = useRef(onFile);
  onFileRef.current = onFile;

  return useCallback(() => {
    const input = document.createElement("input");
    input.type = "file";
    input.multiple = true;
    input.hidden = true;
    // Safari не открывает выбор у инпута вне документа.
    document.body.appendChild(input);

    const cleanup = () => input.remove();
    input.addEventListener(
      "change",
      () => {
        Array.from(input.files ?? []).forEach((file) =>
          onFileRef.current(file)
        );
        cleanup();
      },
      { once: true }
    );
    input.addEventListener("cancel", cleanup, { once: true });
    input.click();
  }, []);
};

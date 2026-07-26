const curlAttachmentFileName = "curl-command.txt";
const curlAttachmentMimeType = "text/plain;charset=utf-8";
const codeFencePattern = /^```[^\n]*\s*([\s\S]*?)\s*```$/;
const shellPromptPattern = /^\s*[$>]\s+(?=curl(?:\s|$))/i;
const continuationPromptPattern = /^\s*>\s+(?=\S)/;
const curlCommandPattern = /^curl(?:\s|$)/i;

export const getClipboardFiles = (
  clipboardData?: DataTransfer | null
): File[] => {
  const files = Array.from(clipboardData?.files ?? []);
  if (files.length > 0) return files;

  return Array.from(clipboardData?.items ?? []).reduce<File[]>((acc, item) => {
    if (item.kind !== "file") return acc;

    const file = item.getAsFile();
    if (!file) return acc;

    return [...acc, file];
  }, []);
};

/**
 * Copy text to clipboard with fallback for older browsers
 */
export async function copyToClipboard(text: string): Promise<void> {
  if (!text) {
    throw new Error("No text to copy");
  }

  try {
    // Try modern clipboard API first
    await navigator.clipboard.writeText(text);
  } catch (err) {
    console.error("Clipboard API failed:", err);

    // Fallback method for older browsers
    try {
      const textArea = document.createElement("textarea");
      textArea.value = text;
      textArea.style.position = "fixed";
      textArea.style.left = "-999999px";
      document.body.appendChild(textArea);
      textArea.focus();
      textArea.select();
      document.execCommand("copy");
      document.body.removeChild(textArea);
    } catch (fallbackErr) {
      console.error("Fallback copy failed:", fallbackErr);
      throw new Error("Не удалось скопировать ссылку", {
        cause: fallbackErr,
      });
    }
  }
}

export const normalizeCurlCommand = (text: string): string | null => {
  const trimmedText = text.trim();
  if (!trimmedText) return null;

  const codeFenceMatch = trimmedText.match(codeFencePattern);
  const commandText = codeFenceMatch?.[1]?.trim() ?? trimmedText;
  const lines = commandText.split(/\r?\n/);
  const startsWithShellPrompt = shellPromptPattern.test(lines[0] ?? "");
  const withoutPrompt = startsWithShellPrompt
    ? lines
        .map((line, index) =>
          index === 0
            ? line.replace(shellPromptPattern, "")
            : line.replace(continuationPromptPattern, "")
        )
        .join("\n")
    : commandText;

  if (!curlCommandPattern.test(withoutPrompt)) return null;

  return withoutPrompt.endsWith("\n") ? withoutPrompt : `${withoutPrompt}\n`;
};

export const createCurlAttachmentFile = (text: string): File | null => {
  const curlCommand = normalizeCurlCommand(text);
  if (!curlCommand) return null;

  return new File([curlCommand], curlAttachmentFileName, {
    type: curlAttachmentMimeType,
  });
};

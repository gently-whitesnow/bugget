const attachmentOnlyText = "Файл прикреплен";

/** Текст записи обязателен по контракту, а отправить можно и одни файлы. */
export const composerText = (text: string) => text.trim() || attachmentOnlyText;

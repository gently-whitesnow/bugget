export const attachmentOnlyText = "Файл прикреплен";

type Params = {
  text: string;
  files: File[];
  create: (text: string) => Promise<{ id?: number } | null | undefined>;
  upload: (entityId: number, file: File) => Promise<unknown>;
  remove: (entityId: number) => unknown;
};

/**
 * Комментарий и шаг создаются отдельно от вложений. Если текста не было и ни
 * один файл не загрузился, запись с подставленным «Файл прикреплен» пустая —
 * её удаляем. Ошибку загрузки показывает эффект вложения.
 */
export const createWithAttachments = async ({
  text,
  files,
  create,
  upload,
  remove,
}: Params): Promise<void> => {
  const trimmed = text.trim();
  const created = await create(trimmed || attachmentOnlyText);
  if (!created?.id || files.length === 0) return;

  let uploaded = 0;
  for (const file of files) {
    try {
      await upload(created.id, file);
      uploaded += 1;
    } catch {
      // Остальные файлы всё равно пробуем загрузить.
    }
  }

  if (uploaded === 0 && !trimmed) {
    await remove(created.id);
  }
};

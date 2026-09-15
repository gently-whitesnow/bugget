export const attachmentOnlyText = "Файл прикреплен";

type Params = {
  text: string;
  files: File[];
  create: (text: string) => Promise<{ id?: number } | null | undefined>;
  upload: (entityId: number, file: File) => Promise<unknown>;
  remove: (entityId: number) => unknown;
};

/**
 * Комментарий и шаг создаются отдельно от вложений, поэтому атомарность
 * держим на фронте: если файл не загрузился, запись удаляем, а форма
 * сохраняет введённое для повторной отправки. Ошибку загрузки показывает
 * эффект вложения.
 *
 * @returns true, если запись создана со всеми файлами.
 */
export const createWithAttachments = async ({
  text,
  files,
  create,
  upload,
  remove,
}: Params): Promise<boolean> => {
  const created = await create(text.trim() || attachmentOnlyText);
  if (!created?.id) return false;

  try {
    for (const file of files) {
      await upload(created.id, file);
    }
    return true;
  } catch {
    await remove(created.id);
    return false;
  }
};

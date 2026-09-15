import { describe, expect, it, vi } from "vitest";

import {
  attachmentOnlyText,
  createWithAttachments,
} from "./createWithAttachments";

const file = (name: string) => new File(["x"], name, { type: "text/plain" });

const setup = (uploadResults: boolean[]) => {
  const create = vi.fn(async () => ({ id: 7 }));
  let call = 0;
  const upload = vi.fn(async () => {
    if (!uploadResults[call++]) throw new Error("400");
  });
  const remove = vi.fn();
  return { create, upload, remove };
};

describe("createWithAttachments", () => {
  it("удаляет пустую запись, если файл не загрузился", async () => {
    const units = setup([false]);

    const sent = await createWithAttachments({
      text: "  ",
      files: [file("a.txt")],
      ...units,
    });

    expect(units.create).toHaveBeenCalledWith(attachmentOnlyText);
    expect(units.remove).toHaveBeenCalledWith(7);
    expect(sent).toBe(false);
  });

  it("удаляет и запись с текстом, чтобы повтор не создал дубль", async () => {
    const units = setup([false]);

    const sent = await createWithAttachments({
      text: "Смотри лог",
      files: [file("a.txt")],
      ...units,
    });

    expect(units.create).toHaveBeenCalledWith("Смотри лог");
    expect(units.remove).toHaveBeenCalledWith(7);
    expect(sent).toBe(false);
  });

  it("при ошибке одного файла не грузит остальные и удаляет запись", async () => {
    const units = setup([true, false, true]);

    const sent = await createWithAttachments({
      text: "",
      files: [file("a.txt"), file("b.txt"), file("c.txt")],
      ...units,
    });

    expect(units.upload).toHaveBeenCalledTimes(2);
    expect(units.remove).toHaveBeenCalledWith(7);
    expect(sent).toBe(false);
  });

  it("сообщает об успехе, если загрузились все файлы", async () => {
    const units = setup([true, true]);

    const sent = await createWithAttachments({
      text: "",
      files: [file("a.txt"), file("b.txt")],
      ...units,
    });

    expect(units.remove).not.toHaveBeenCalled();
    expect(sent).toBe(true);
  });

  it("без файлов ничего не загружает и не удаляет", async () => {
    const units = setup([]);

    const sent = await createWithAttachments({
      text: "текст",
      files: [],
      ...units,
    });

    expect(units.upload).not.toHaveBeenCalled();
    expect(units.remove).not.toHaveBeenCalled();
    expect(sent).toBe(true);
  });

  it("не отправлено, если запись не создалась", async () => {
    const units = setup([]);
    units.create.mockResolvedValueOnce(null as never);

    const sent = await createWithAttachments({
      text: "текст",
      files: [file("a.txt")],
      ...units,
    });

    expect(units.upload).not.toHaveBeenCalled();
    expect(sent).toBe(false);
  });
});

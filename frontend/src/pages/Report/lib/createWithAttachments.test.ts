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
  it("удаляет пустую запись, если ни один файл не загрузился", async () => {
    const units = setup([false]);

    await createWithAttachments({
      text: "  ",
      files: [file("a.txt")],
      ...units,
    });

    expect(units.create).toHaveBeenCalledWith(attachmentOnlyText);
    expect(units.remove).toHaveBeenCalledWith(7);
  });

  it("оставляет запись с текстом, даже если файлы не загрузились", async () => {
    const units = setup([false]);

    await createWithAttachments({
      text: "Смотри лог",
      files: [file("a.txt")],
      ...units,
    });

    expect(units.create).toHaveBeenCalledWith("Смотри лог");
    expect(units.remove).not.toHaveBeenCalled();
  });

  it("не удаляет запись, если загрузился хотя бы один файл", async () => {
    const units = setup([false, true]);

    await createWithAttachments({
      text: "",
      files: [file("a.txt"), file("b.txt")],
      ...units,
    });

    expect(units.upload).toHaveBeenCalledTimes(2);
    expect(units.remove).not.toHaveBeenCalled();
  });

  it("без файлов ничего не загружает и не удаляет", async () => {
    const units = setup([]);

    await createWithAttachments({ text: "текст", files: [], ...units });

    expect(units.upload).not.toHaveBeenCalled();
    expect(units.remove).not.toHaveBeenCalled();
  });
});

import type { Attachment } from "@/entities/report";

type AttachmentOwner = { id: number; attachments?: Attachment[] | null };

/** Правит вложения одной сущности бага (шага или комментария) в сторе «id бага → сущности». */
export const patchOwnerAttachments = <T extends AttachmentOwner>(
  state: Record<number, T[]>,
  bugId: number,
  ownerId: number,
  patch: (attachments: Attachment[]) => Attachment[]
): Record<number, T[]> => ({
  ...state,
  [bugId]: (state[bugId] || []).map((owner) =>
    owner.id === ownerId
      ? { ...owner, attachments: patch(owner.attachments || []) }
      : owner
  ),
});

export const withoutAttachment =
  (attachmentId: number) => (attachments: Attachment[]) =>
    attachments.filter((a) => a.id !== attachmentId);

export const withRenamedAttachment =
  (attachment: Attachment) => (attachments: Attachment[]) =>
    attachments.map((item) =>
      item.id === attachment.id ? { ...item, ...attachment } : item
    );

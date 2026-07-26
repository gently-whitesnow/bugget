import { AttachmentTypes } from "@/shared/config";

export type Attachment = {
  id: number;
  entityId: number;
  attachType: AttachmentTypes;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

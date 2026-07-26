import { Attachment } from "./attachment";

export type Comment = {
  id: number;
  bugId: number;
  text: string;
  creatorUserId: string;
  createdAt: string;
  creatorType: number;
  audience: number;
  updatedAt: string;
  attachments?: Attachment[] | null;
};

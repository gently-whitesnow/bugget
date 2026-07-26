import { Attachment } from "./attachment";

export type BugStep = {
  id: number;
  bugId: number;
  text: string;
  stepNumber: number;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  attachments: Attachment[] | null;
};

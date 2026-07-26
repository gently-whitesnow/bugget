import { BugStatuses } from "@/shared/config";

import { Attachment } from "./attachment";
import { Comment } from "./comment";

export type BugClientEntity = {
  id: number;
  reportId: string;
  title: string | null;
  receive: string | null;
  expect: string | null;
  creatorUserId: string;
  creatorType?: number;
  createdAt: string;
  updatedAt: string;
  status: BugStatuses;
  attachments: Attachment[] | null;
  comments: Comment[] | null;
  clientId: number;
  isLocalOnly: boolean;
};

export type BugFormData = {
  title?: string | null;
  receive?: string;
  expect?: string;
  status?: BugStatuses;
};

export type BugUpdateData = {
  bugId: number;
  reportId: string;
  data: BugFormData;
};

export type ResultFieldTypes = "receive" | "expect";

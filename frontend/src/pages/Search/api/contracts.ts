import type { BugStatuses, ReportStatuses } from "@/shared/config";

export type AttachmentResponse = {
  id: number;
  bugId: number;
  reportId: string;
  path: string | null;
  createdAt: string;
  attachType: number;
};

export type BugResponse = {
  id: number;
  reportId: string;
  receive: string | null;
  expect: string | null;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  status: BugStatuses;
  attachments: AttachmentResponse[];
  comments: CommentResponse[];
};

export type CommentResponse = {
  id: number;
  bugId: number;
  text: string | null;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
};

export type ReportResponse = {
  id: string;
  title: string;
  status: ReportStatuses;
  responsibleUserId: string;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  participantsUserIds: string[] | null;
  bugs: BugResponse[] | null;
};

export type SearchResponse = {
  reports: ReportResponse[];
  total: number;
};

export type SearchRequestQueryParams = {
  query?: string;
  reportStatuses?: number[];
  userId?: string;
  teamId?: string;
  sort?: string;
  skip?: number;
  take?: number;
  creatorTypes?: number[];
};

import { ReportStatuses } from "@/shared/config";

export type Report = {
  id: string;
  title: string;
  status: ReportStatuses;
  responsibleUserId: string;
  pastResponsibleUserId: string;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
};

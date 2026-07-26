export type ExternalSearchItem = {
  id: string;
  text: string;
  source: string;
};

export type ExternalSearchResponse = {
  total: number;
  items: ExternalSearchItem[];
};

export type ExternalSearchApplyRequest = {
  id: string;
  source: string;
  reportId: string;
};

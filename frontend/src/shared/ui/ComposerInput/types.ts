export type PendingAttachment = {
  id: number;
  file: File;
  name: string;
  kind: "file" | "curl";
  previewUrl?: string | null;
};

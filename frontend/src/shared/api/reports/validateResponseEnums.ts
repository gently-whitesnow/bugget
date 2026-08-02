import type { paths } from "@/shared/api/generated/reports";
import type { MethodsOf, ResponseValidator } from "@/shared/api/operation";
import {
  AttachmentTypes,
  BugStatuses,
  CommentAudiences,
  CreatorTypes,
  ReportStatuses,
} from "./enumValues";

type Decoder = (value: unknown) => void;
type ReportsOperation = {
  [P in keyof paths & string]: `${MethodsOf<paths[P]>} ${P}`;
}[keyof paths & string];

const allowed = {
  attachType: new Set(Object.values(AttachmentTypes)),
  audience: new Set(Object.values(CommentAudiences)),
  bugStatus: new Set(Object.values(BugStatuses)),
  creatorType: new Set(Object.values(CreatorTypes)),
  reportStatus: new Set(Object.values(ReportStatuses)),
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const assertField = (
  value: unknown,
  field: string,
  values: ReadonlySet<unknown>
) => {
  if (isRecord(value) && field in value && !values.has(value[field])) {
    throw new TypeError(`Неизвестное значение reports HTTP response: ${field}`);
  }
};

const each = (value: unknown, decoder: Decoder) => {
  if (Array.isArray(value)) value.forEach(decoder);
};

const decodeAttachment: Decoder = (value) =>
  assertField(value, "attachType", allowed.attachType);

const decodeComment: Decoder = (value) => {
  assertField(value, "creatorType", allowed.creatorType);
  assertField(value, "audience", allowed.audience);
  if (isRecord(value)) each(value.attachments, decodeAttachment);
};

const decodeStep: Decoder = (value) => {
  if (isRecord(value)) each(value.attachments, decodeAttachment);
};

const decodeBug: Decoder = (value) => {
  assertField(value, "status", allowed.bugStatus);
  assertField(value, "creatorType", allowed.creatorType);
  if (!isRecord(value)) return;
  each(value.attachments, decodeAttachment);
  each(value.comments, decodeComment);
  each(value.steps, decodeStep);
};

const decodeReport: Decoder = (value) => {
  assertField(value, "status", allowed.reportStatus);
  assertField(value, "creatorType", allowed.creatorType);
  if (isRecord(value)) each(value.bugs, decodeBug);
};

const decodeReportList: Decoder = (value) => {
  if (isRecord(value)) each(value.reports, decodeReport);
};

const decoders = {
  "get /v2/reports": decodeReportList,
  "post /v2/reports": decodeReport,
  "get /v2/reports/{aliasId}": decodeReport,
  "patch /v2/reports/{aliasId}": decodeReport,
  "get /v1/reports/search": decodeReportList,
  "post /v2/reports/{aliasId}/bugs": decodeBug,
  "patch /v2/reports/{aliasId}/bugs/{bugId}": decodeBug,
  "post /v2/reports/{aliasId}/bugs/{bugId}/steps": decodeStep,
  "put /v2/reports/{aliasId}/bugs/{bugId}/steps/order": (value) =>
    each(value, decodeStep),
  "patch /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}": decodeStep,
  "post /v2/reports/{aliasId}/bugs/{bugId}/comments": decodeComment,
  "put /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}": decodeComment,
  "post /v2/reports/{aliasId}/bugs/{bugId}/attachments": decodeAttachment,
  "patch /v2/reports/{aliasId}/bugs/{bugId}/attachments/{id}": decodeAttachment,
  "post /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments":
    decodeAttachment,
  "patch /v2/reports/{aliasId}/bugs/{bugId}/steps/{stepId}/attachments/{id}":
    decodeAttachment,
  "post /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments":
    decodeAttachment,
  "patch /v2/reports/{aliasId}/bugs/{bugId}/comments/{commentId}/attachments/{id}":
    decodeAttachment,
} satisfies Partial<Record<ReportsOperation, Decoder>>;

/** Проверяет enum-позиции decoder'ом конкретной OpenAPI-операции. */
export const validateReportsResponseEnums: ResponseValidator = (
  value,
  { path, method }
) => {
  const decoder = decoders[`${method} ${path}` as keyof typeof decoders];
  decoder?.(value);
};

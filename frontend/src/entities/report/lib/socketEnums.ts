import {
  AttachmentTypes,
  BugStatuses,
  CommentAudiences,
  CreatorTypes,
  ReportStatuses,
} from "@/shared/config";

/**
 * Числовые enum'ы SignalR → значения провода.
 *
 * HTTP перешёл на строки `snake_case` (ADR-0013), realtime-контракт остался
 * числовым и меняется отдельным решением (ADR-0007). Шов между ними — здесь, а
 * не в компонентах: иначе на странице репорта одно и то же поле имело бы два
 * представления в зависимости от того, каким путём приехало.
 *
 * Порядок значений повторяет числа домена. Неизвестное число не подменяется
 * «ближайшим»: молча показать чужой статус хуже, чем упасть на шве.
 */
const reportStatuses = [
  ReportStatuses.BACKLOG,
  ReportStatuses.RESOLVED,
  ReportStatuses.FIX,
  ReportStatuses.REJECTED,
  ReportStatuses.TEST,
] as const;

const bugStatuses = [
  BugStatuses.OPEN,
  BugStatuses.VERIFIED,
  BugStatuses.REJECTED,
  BugStatuses.FIXED,
] as const;

const creatorTypes = [
  CreatorTypes.USER,
  CreatorTypes.SYSTEM,
  CreatorTypes.TG_BETA_TESTER,
] as const;

const commentAudiences = [
  CommentAudiences.INTERNAL,
  CommentAudiences.EXTERNAL,
] as const;

const attachmentTypes = [
  AttachmentTypes.FACT,
  AttachmentTypes.EXPECT,
  AttachmentTypes.COMMENT,
  AttachmentTypes.BUG_STEP,
] as const;

const decode = <T extends string>(
  values: readonly T[],
  value: number,
  field: string
): T => {
  const decoded = values[value];
  if (decoded === undefined) {
    throw new Error(
      `Realtime-событие принесло неизвестное значение ${field}: ${value}.`
    );
  }
  return decoded;
};

export const reportStatusFromSocket = (value: number): ReportStatuses =>
  decode(reportStatuses, value, "status репорта");

export const bugStatusFromSocket = (value: number): BugStatuses =>
  decode(bugStatuses, value, "status бага");

export const creatorTypeFromSocket = (value: number): CreatorTypes =>
  decode(creatorTypes, value, "creatorType");

export const commentAudienceFromSocket = (value: number): CommentAudiences =>
  decode(commentAudiences, value, "audience");

export const attachTypeFromSocket = (value: number): AttachmentTypes =>
  decode(attachmentTypes, value, "attachType");

/**
 * Разведение вложений по владельцу: события всех трёх семейств приходят одной
 * формой, и адресат определяется только типом.
 */
export const isCommentAttachment = (value: number): boolean =>
  attachTypeFromSocket(value) === AttachmentTypes.COMMENT;

export const isBugStepAttachment = (value: number): boolean =>
  attachTypeFromSocket(value) === AttachmentTypes.BUG_STEP;

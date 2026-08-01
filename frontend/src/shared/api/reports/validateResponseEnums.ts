import {
  AttachmentTypes,
  BugStatuses,
  CommentAudiences,
  CreatorTypes,
  ReportStatuses,
} from "@/shared/config/const";

const knownValues = {
  attachType: new Set(Object.values(AttachmentTypes)),
  audience: new Set(Object.values(CommentAudiences)),
  creatorType: new Set(Object.values(CreatorTypes)),
};

const reportStatuses = new Set(Object.values(ReportStatuses));
const bugStatuses = new Set(Object.values(BugStatuses));

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const assertKnownValue = (
  object: Record<string, unknown>,
  field: string,
  allowed: ReadonlySet<unknown>
) => {
  if (field in object && !allowed.has(object[field])) {
    throw new TypeError(`Неизвестное значение reports HTTP response: ${field}`);
  }
};

/**
 * Runtime-проверка закрытых enum-полей ответа reports до передачи потребителям.
 * Axios уже перевёл ключи в camelCase; значения при этом остаются значениями
 * провода. Строковый id отличает Report от числового Bug во всех их response-
 * формах, включая summary/patch/list.
 */
export const validateReportsResponseEnums = (value: unknown): void => {
  if (Array.isArray(value)) {
    value.forEach(validateReportsResponseEnums);
    return;
  }
  if (!isRecord(value)) return;

  for (const [field, allowed] of Object.entries(knownValues)) {
    assertKnownValue(value, field, allowed);
  }

  if ("status" in value && typeof value.id === "string") {
    assertKnownValue(value, "status", reportStatuses);
  } else if ("status" in value && typeof value.id === "number") {
    assertKnownValue(value, "status", bugStatuses);
  }

  Object.values(value).forEach(validateReportsResponseEnums);
};

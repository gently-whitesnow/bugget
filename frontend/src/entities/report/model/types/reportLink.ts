import type { reportsApi } from "@/shared/api";
import type { ReportLinkWire } from "./wire";

/**
 * Ссылка репорта — формы ответа и тела запроса из контракта модуля `reports`,
 * выведенные из операций, а не описанные руками (ADR-0009).
 *
 * В ответе есть `reportId`: раньше рукописный тип его не объявлял, и живое поле
 * провода было не видно из кода.
 */
export type ReportLink = ReportLinkWire;

export type ReportLinkDto = reportsApi.ReportLinkBody;

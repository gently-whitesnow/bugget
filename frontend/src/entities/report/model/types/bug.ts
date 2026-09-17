import { BugStatuses } from "@/shared/config";
import type { BugWire } from "./wire";

/**
 * Баг в сторе: форма провода из контракта (ADR-0009) плюс клиентские поля.
 *   * `steps` нет: у шагов свой стор (`pages/Report/model-bug-step`);
 *   * `reportId` — alias репорта (`<team>-<номер>`), ключ группировки в сторе;
 *     числовой `Bug.report_id` провода подставлять сюда нельзя;
 *   * `clientId` и `isLocalOnly` — оптимистичный баг, которого на сервере ещё нет.
 */
export type BugClientEntity = Omit<BugWire, "steps" | "reportId"> & {
  reportId: string;
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

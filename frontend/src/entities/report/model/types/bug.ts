import { BugStatuses } from "@/shared/config";
import type { BugWire } from "./wire";

/**
 * Баг в сторе страницы репорта: форма из контракта плюс клиентские поля.
 *
 * Форма провода выведена из yaml (ADR-0009), а не описана руками. Отличия от
 * `Bug` — намеренные и только клиентские:
 *
 *   * `steps` не хранятся здесь: у шагов свой стор (`pages/Report/model-bug-step`),
 *     и держать их в двух местах — расхождение по построению;
 *   * `reportId` — alias репорта (`report.id`, строка вида `<team>-<номер>`), по
 *     которому стор группирует баги. На проводе `Bug.report_id` — числовой id той
 *     же сущности, и подставлять его сюда нельзя: ключ стора перестанет
 *     совпадать с ключом, по которому баги ищут;
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

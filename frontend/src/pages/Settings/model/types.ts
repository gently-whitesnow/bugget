import { SettingTypes } from "@/shared/config";
import type { KaitenBoardResponse } from "../api/contracts";

export type SettingType =
  | SettingTypes.WORKSPACE
  | SettingTypes.TEAM
  | SettingTypes.USER;

/**
 * Доска в сторе — та же доска, что отдаёт контракт модуля `external`. Своего
 * усечённого DTO здесь нет: он расходился бы с контрактом молча.
 */
export type KaitenBoard = KaitenBoardResponse;

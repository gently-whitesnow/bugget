import { SettingTypes } from "@/shared/config";

export type SettingType =
  | SettingTypes.WORKSPACE
  | SettingTypes.TEAM
  | SettingTypes.USER;

export type KaitenBoard = {
  id: number;
  title: string;
};

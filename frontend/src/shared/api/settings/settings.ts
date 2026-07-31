import { request } from "./client";
import type { Body, Result } from "./client";

/* ── Разделы настроек ──────────────────────────────────────────────────────── */

export type SettingsSectionsResult = Result<"/v1/settings-sections", "get">;

export const fetchSettingsSections = () =>
  request("/v1/settings-sections", "get", {});

/* ── Значение настройки ────────────────────────────────────────────────────── */

const WORKSPACE_SETTING =
  "/v1/workspace-settings-sections/{sectionId}/settings/{settingId}";
const TEAM_SETTING =
  "/v1/team-settings-sections/{sectionId}/settings/{settingId}";
const USER_SETTING =
  "/v1/user-settings-sections/{sectionId}/settings/{settingId}";

/**
 * Три ручки обновления отличаются только уровнем — и адресом. Тело, сегменты и
 * ответ у них одинаковые, поэтому типы берутся у одной из операций.
 */
export type SettingResult = Result<typeof WORKSPACE_SETTING, "put">;
export type SettingValuesBody = Body<typeof WORKSPACE_SETTING, "put">;

export const updateWorkspaceSetting = (
  sectionId: string,
  settingId: string,
  values: SettingValuesBody
) =>
  request(WORKSPACE_SETTING, "put", {
    path: { sectionId, settingId },
    body: values,
  });

export const updateTeamSetting = (
  sectionId: string,
  settingId: string,
  values: SettingValuesBody
) =>
  request(TEAM_SETTING, "put", {
    path: { sectionId, settingId },
    body: values,
  });

export const updateUserSetting = (
  sectionId: string,
  settingId: string,
  values: SettingValuesBody
) =>
  request(USER_SETTING, "put", {
    path: { sectionId, settingId },
    body: values,
  });

import { appApi } from "@/shared/api";
import type {
  SettingsSectionsResponse,
  WorkspaceSettingView,
  TeamSettingView,
  UserSettingView,
} from "./contracts";

export async function fetchSettingsSections(): Promise<SettingsSectionsResponse> {
  const { data } = await appApi.get<SettingsSectionsResponse>(
    "/v1/settings-sections"
  );
  return data;
}

export async function updateWorkspaceSetting(
  sectionId: string,
  settingId: string,
  values: string[]
): Promise<WorkspaceSettingView> {
  const { data } = await appApi.put<WorkspaceSettingView>(
    `/v1/workspace-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

export async function updateTeamSetting(
  sectionId: string,
  settingId: string,
  values: string[]
): Promise<TeamSettingView> {
  const { data } = await appApi.put<TeamSettingView>(
    `/v1/team-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

export async function updateUserSetting(
  sectionId: string,
  settingId: string,
  values: string[]
): Promise<UserSettingView> {
  const { data } = await appApi.put<UserSettingView>(
    `/v1/user-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

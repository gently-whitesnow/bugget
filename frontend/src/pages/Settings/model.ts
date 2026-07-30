import { createStore, createEffect, createEvent, sample } from "effector";
import { fetchKaitenBoards, searchKaitenBoards } from "./api/kaiten";
import type { KaitenBoard } from "./model/types";
import {
  fetchSettingsSections,
  updateTeamSetting,
  updateUserSetting,
  updateWorkspaceSetting,
} from "./api/settings";
import type { SettingValues, SettingsSectionsResponse } from "./api/contracts";
import { notificationMessages, notifyErrorRequested } from "@/shared/model";

// Kaiten Boards
// Effects
export const searchBoardsFx = createEffect(
  async (query?: string): Promise<KaitenBoard[]> => {
    return await searchKaitenBoards(query);
  }
);

export const loadBoardsByIdsFx = createEffect(
  async (ids: string[]): Promise<KaitenBoard[]> => {
    const numericIds = ids
      .map((id) => parseInt(id, 10))
      .filter((id) => !isNaN(id));

    if (numericIds.length === 0) {
      return [];
    }

    return await fetchKaitenBoards(numericIds);
  }
);

// Stores
export const $searchResults = createStore<KaitenBoard[]>([]).on(
  searchBoardsFx.doneData,
  (_, boards) => boards
);

export const $selectedBoards = createStore<KaitenBoard[]>([]).on(
  loadBoardsByIdsFx.doneData,
  (_, boards) => boards
);

export const $isSearching = searchBoardsFx.pending;
export const $isLoadingSelected = loadBoardsByIdsFx.pending;

// Settings
// Types
type SettingsStore = {
  data: SettingsSectionsResponse | null;
  loading: boolean;
  error: string | null;
};

type UpdateSettingParams = {
  sectionId: string;
  settingId: string;
  values: SettingValues;
};

// Events
export const resetSettings = createEvent();

// Effects
export const fetchSettingsFx = createEffect(async () => {
  return await fetchSettingsSections();
});

export const updateWorkspaceSettingFx = createEffect(
  async (params: UpdateSettingParams) => {
    try {
      return await updateWorkspaceSetting(
        params.sectionId,
        params.settingId,
        params.values
      );
    } catch (error) {
      notifyErrorRequested({
        title: "Не удалось сохранить настройки workspace",
        message: notificationMessages.errorRetry,
        options: {
          dedupeKey: "settings-workspace-update-failed",
        },
      });
      throw error;
    }
  }
);

export const updateTeamSettingFx = createEffect(
  async (params: UpdateSettingParams) => {
    try {
      return await updateTeamSetting(
        params.sectionId,
        params.settingId,
        params.values
      );
    } catch (error) {
      notifyErrorRequested({
        title: "Не удалось сохранить настройки команды",
        message: notificationMessages.errorRetry,
        options: {
          dedupeKey: "settings-team-update-failed",
        },
      });
      throw error;
    }
  }
);

export const updateUserSettingFx = createEffect(
  async (params: UpdateSettingParams) => {
    try {
      return await updateUserSetting(
        params.sectionId,
        params.settingId,
        params.values
      );
    } catch (error) {
      notifyErrorRequested({
        title: "Не удалось сохранить пользовательские настройки",
        message: notificationMessages.errorRetry,
        options: {
          dedupeKey: "settings-user-update-failed",
        },
      });
      throw error;
    }
  }
);

// Store
export const $settings = createStore<SettingsStore>({
  data: null,
  loading: false,
  error: null,
})
  .on(fetchSettingsFx.pending, (state, pending) => ({
    ...state,
    loading: pending,
    error: pending ? null : state.error,
  }))
  .on(fetchSettingsFx.doneData, (state, data) => ({
    ...state,
    data,
    loading: false,
    error: null,
  }))
  .on(fetchSettingsFx.fail, (state, { error }) => ({
    ...state,
    loading: false,
    error: error.message || "Ошибка загрузки настроек",
  }))
  .reset(resetSettings);

// Update workspace setting in store
sample({
  clock: updateWorkspaceSettingFx.done,
  source: $settings,
  fn: (state, { params, result }): SettingsStore => {
    if (!state.data) return state;

    return {
      ...state,
      data: {
        ...state.data,
        workspaceSections: state.data.workspaceSections.map((section) =>
          section.id === params.sectionId
            ? {
                ...section,
                settings: section.settings.map((setting) =>
                  setting.id === params.settingId
                    ? { ...setting, values: result.values ?? params.values }
                    : setting
                ),
              }
            : section
        ),
      },
    };
  },
  target: $settings,
});

// Update team setting in store
sample({
  clock: updateTeamSettingFx.done,
  source: $settings,
  fn: (state, { params, result }): SettingsStore => {
    if (!state.data) return state;

    return {
      ...state,
      data: {
        ...state.data,
        teamSections: state.data.teamSections.map((section) =>
          section.id === params.sectionId
            ? {
                ...section,
                settings: section.settings.map((setting) =>
                  setting.id === params.settingId
                    ? { ...setting, values: result.values ?? params.values }
                    : setting
                ),
              }
            : section
        ),
      },
    };
  },
  target: $settings,
});

// Update user setting in store
sample({
  clock: updateUserSettingFx.done,
  source: $settings,
  fn: (state, { params, result }): SettingsStore => {
    if (!state.data) return state;

    return {
      ...state,
      data: {
        ...state.data,
        userSections: state.data.userSections.map((section) =>
          section.id === params.sectionId
            ? {
                ...section,
                settings: section.settings.map((setting) =>
                  setting.id === params.settingId
                    ? { ...setting, values: result.values ?? params.values }
                    : setting
                ),
              }
            : section
        ),
      },
    };
  },
  target: $settings,
});

// Derived stores for pending states
export const $isUpdatingWorkspace = updateWorkspaceSettingFx.pending;
export const $isUpdatingTeam = updateTeamSettingFx.pending;
export const $isUpdatingUser = updateUserSettingFx.pending;

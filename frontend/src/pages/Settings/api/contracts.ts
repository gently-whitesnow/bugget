export type SettingView = {
  id: string;
  title: string;
  description?: string;
  isArray: boolean;
  isBool: boolean;
  values: string[];
};

export type WorkspaceSettingView = SettingView;
export type TeamSettingView = SettingView;
export type UserSettingView = SettingView;

export type SettingsSectionView<T extends SettingView = SettingView> = {
  id: string;
  title: string;
  settings: T[];
};

export type WorkspaceSettingsSectionView =
  SettingsSectionView<WorkspaceSettingView>;
export type TeamSettingsSectionView = SettingsSectionView<TeamSettingView>;
export type UserSettingsSectionView = SettingsSectionView<UserSettingView>;

export type SettingsSectionsResponse = {
  workspaceSections: WorkspaceSettingsSectionView[];
  teamSections: TeamSettingsSectionView[];
  userSections: UserSettingsSectionView[];
};

export type UpdateSettingRequest = {
  sectionId: string;
  settingId: string;
  values: string[];
};

export type KaitenBoardResponse = {
  id: number;
  title: string;
};

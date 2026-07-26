import { useEffect, useCallback } from "react";
import { useSearchParams } from "react-router-dom";
import { useUnit } from "effector-react";
import { AlertTriangle } from "lucide-react";
import {
  $settings,
  fetchSettingsFx,
  updateWorkspaceSettingFx,
  updateTeamSettingFx,
  updateUserSettingFx,
  $isUpdatingWorkspace,
  $isUpdatingTeam,
  $isUpdatingUser,
} from "../model";

import {
  externalProviders,
  internalProviders,
  SettingTypes,
  showExternalProviders,
  showInternalProviders,
} from "@/shared/config";
import { $authUserStore, isAdmin } from "@/entities/user";
import type { SettingType } from "../model/types";
import { SettingsHeader } from "./components/SettingsHeader/SettingsHeader";
import { SettingsTabs } from "./components/SettingsTabs/SettingsTabs";
import { SettingsSection } from "./components/SettingsSection/SettingsSection";
import { UserProfileSection } from "./components/UserProfileSection/UserProfileSection";
import { useExternalLinks } from "./hooks/useExternalLinks";
import { useUserProfile } from "./hooks/useUserProfile";

const validTabs: SettingType[] = [
  SettingTypes.WORKSPACE,
  SettingTypes.TEAM,
  SettingTypes.USER,
];
const tabQueryParam = "tab";

const isValidTab = (tab: string | null): tab is SettingType => {
  return tab !== null && validTabs.includes(tab as SettingType);
};

export const Settings = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const tabFromUrl = searchParams.get(tabQueryParam);
  const activeTab: SettingType = isValidTab(tabFromUrl)
    ? tabFromUrl
    : SettingTypes.WORKSPACE;

  const handleTabChange = useCallback(
    (tab: SettingType) => {
      setSearchParams({ [tabQueryParam]: tab }, { replace: true });
    },
    [setSearchParams]
  );

  const [
    settings,
    isUpdatingWorkspace,
    isUpdatingTeam,
    isUpdatingUser,
    authUser,
  ] = useUnit([
    $settings,
    $isUpdatingWorkspace,
    $isUpdatingTeam,
    $isUpdatingUser,
    $authUserStore,
  ]);

  const {
    fetchSettings,
    updateWorkspaceSetting,
    updateTeamSetting,
    updateUserSetting,
  } = useUnit({
    fetchSettings: fetchSettingsFx,
    updateWorkspaceSetting: updateWorkspaceSettingFx,
    updateTeamSetting: updateTeamSettingFx,
    updateUserSetting: updateUserSettingFx,
  });

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const profile = useUserProfile();

  const {
    externalLinks,
    isExternalLinksLoading,
    unlinkingProvider,
    handleProviderLink,
    handleProviderUnlink,
    showMergeDialog,
    isMerging,
    handleMergeConfirm,
    handleMergeCancel,
  } = useExternalLinks();

  const handleUpdate = useCallback(
    (
      type: SettingType,
      sectionId: string,
      settingId: string,
      values: string[]
    ) => {
      switch (type) {
        case SettingTypes.WORKSPACE:
          updateWorkspaceSetting({ sectionId, settingId, values });
          break;
        case SettingTypes.TEAM:
          updateTeamSetting({ sectionId, settingId, values });
          break;
        case SettingTypes.USER:
          updateUserSetting({ sectionId, settingId, values });
          break;
      }
    },
    [updateWorkspaceSetting, updateTeamSetting, updateUserSetting]
  );

  const filterSections = <T extends { id: string }>(sections: T[]): T[] =>
    sections;

  const userIsAdmin = isAdmin(authUser);

  const getCurrentSections = () => {
    if (!settings.data) return [];
    switch (activeTab) {
      case SettingTypes.WORKSPACE:
        return filterSections(settings.data.workspaceSections);
      case SettingTypes.TEAM:
        return filterSections(settings.data.teamSections);
      case SettingTypes.USER:
        return filterSections(settings.data.userSections);
    }
  };

  const isReadOnly = activeTab === SettingTypes.WORKSPACE && !userIsAdmin;

  const getCurrentUpdatingState = () => {
    switch (activeTab) {
      case SettingTypes.WORKSPACE:
        return isUpdatingWorkspace;
      case SettingTypes.TEAM:
        return isUpdatingTeam;
      case SettingTypes.USER:
        return isUpdatingUser;
    }
  };

  if (settings.loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <div className="loading loading-spinner loading-lg text-primary"></div>
          <p className="mt-4 text-sm text-base-content/60">
            Загрузка настроек...
          </p>
        </div>
      </div>
    );
  }

  if (settings.error) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <AlertTriangle className="mx-auto mb-4 h-10 w-10 text-error" />
          <p className="text-error font-medium">{settings.error}</p>
          <button className="btn btn-ghost btn-sm mt-4" onClick={fetchSettings}>
            Попробовать снова
          </button>
        </div>
      </div>
    );
  }

  const currentSections = getCurrentSections();
  const isUpdating = getCurrentUpdatingState();

  return (
    <div className="layout-content-narrow flex flex-col gap-6">
      <UserProfileSection
        currentUserAvatar={profile.currentUserAvatar}
        currentUserName={profile.currentUserName}
        userInitial={profile.userInitial}
        profileName={profile.profileName}
        profileError={profile.profileError}
        isProfileUpdating={profile.isProfileUpdating}
        hasProfileNameChanges={profile.hasProfileNameChanges}
        mattermostUserId={profile.user?.mattermostUserId ?? null}
        mattermostIdInput={profile.mattermostIdInput}
        isMattermostDisconnecting={profile.isMattermostDisconnecting}
        isMattermostLinking={profile.isMattermostLinking}
        externalLinks={externalLinks}
        isExternalLinksLoading={isExternalLinksLoading}
        unlinkingProvider={unlinkingProvider}
        onAvatarUpload={profile.handleAvatarUpload}
        onAvatarDelete={profile.handleAvatarDelete}
        onProfileNameChange={profile.setProfileName}
        onProfileNameSave={profile.handleProfileNameSave}
        onMattermostDisconnect={profile.handleMattermostDisconnect}
        onMattermostIdInputChange={profile.setMattermostIdInput}
        onMattermostLink={profile.handleMattermostLink}
        showExternalProviders={showExternalProviders}
        externalProviders={externalProviders}
        showInternalProviders={showInternalProviders}
        internalProviders={internalProviders}
        onProviderLink={handleProviderLink}
        onProviderUnlink={handleProviderUnlink}
        showMergeDialog={showMergeDialog}
        isMerging={isMerging}
        onMergeConfirm={handleMergeConfirm}
        onMergeCancel={handleMergeCancel}
      />

      <SettingsHeader />

      <SettingsTabs activeTab={activeTab} onTabChange={handleTabChange} />
      <div className="flex flex-col gap-4 w-full">
        {currentSections.length > 0 &&
          currentSections.map((section) => (
            <SettingsSection
              key={section.id}
              section={section}
              type={activeTab}
              onUpdate={handleUpdate}
              isUpdating={isUpdating}
              readOnly={isReadOnly}
            />
          ))}
        {currentSections.length === 0 &&
          activeTab !== SettingTypes.WORKSPACE && (
            <div className="bg-base-100 rounded-xl border border-base-300/50 p-12 text-center">
              <div className="text-4xl mb-3">📭</div>
              <p className="text-base-content/60">
                Нет доступных настроек в этой категории
              </p>
            </div>
          )}
      </div>
    </div>
  );
};

export default Settings;

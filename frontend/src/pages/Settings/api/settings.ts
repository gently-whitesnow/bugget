import { settingsApi } from "@/shared/api";

/**
 * Настройки глазами страницы. Транспорт живёт в операциях модуля
 * (`shared/api/settings`) — здесь только имена, под которыми их зовёт модель
 * страницы.
 *
 * Собственного дескриптора операции (`settingsRoutes`, `SettingsMethod`,
 * `callContract`) у страницы больше нет: он был вторым представлением механики
 * `shared/api/operation.ts`, и два типовых пути вызова расходились бы молча.
 */
export const fetchSettingsSections = settingsApi.fetchSettingsSections;
export const updateWorkspaceSetting = settingsApi.updateWorkspaceSetting;
export const updateTeamSetting = settingsApi.updateTeamSetting;
export const updateUserSetting = settingsApi.updateUserSetting;

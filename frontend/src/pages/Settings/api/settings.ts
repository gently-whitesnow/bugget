import { appApi } from "@/shared/api";
import type { components, operations } from "@/shared/api/generated/settings";
import type { Camelized } from "@/shared/lib/types";
import type { SettingView, SettingsSectionsResponse } from "./contracts";

/**
 * HTTP-клиенты модуля `settings`.
 *
 * Тело запроса и тело ответа каждой ручки выведены из её операции в контракте
 * (`specs/contracts/settings/openapi.yaml` → `shared/api/generated/settings.d.ts`),
 * а не объявлены рядом рукописным DTO: расхождение с контрактом становится ошибкой
 * компиляции, а не сюрпризом в рантайме у заказчика.
 *
 * Тела ответов приходят в snake_case и перекладываются в camelCase общим
 * интерсептором (`shared/api/instances/base.ts`) — отсюда `Camelized<T>` на ответе.
 * Сегменты пути (`sectionId`, `settingId`) конверсию не проходят: их имена — часть
 * публичного контракта, и типы для них берутся из generated напрямую (ADR-0009).
 *
 * Тело PUT — голый массив строк: именованных полей в нём нет, интерсептор запроса
 * оставляет его как есть.
 */

type SectionId = components["parameters"]["SectionId"];
type SettingId = components["parameters"]["SettingId"];

/** Тело запроса операции ровно так, как его объявляет контракт. */
type RequestBody<O extends keyof operations> =
  operations[O]["requestBody"] extends {
    content: { "application/json": infer B };
  }
    ? B
    : never;

/** Успешный ответ операции в том виде, в каком его отдаёт HTTP-слой фронта. */
type JsonResponse<O extends keyof operations> = Camelized<
  operations[O]["responses"][200]["content"]["application/json"]
>;

export async function fetchSettingsSections(): Promise<SettingsSectionsResponse> {
  const { data } = await appApi.get<
    JsonResponse<"Settings_GetSettingsSections">
  >("/v1/settings-sections");
  return data;
}

export async function updateWorkspaceSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: RequestBody<"Settings_UpdateWorkspaceSetting">
): Promise<SettingView> {
  const { data } = await appApi.put<
    JsonResponse<"Settings_UpdateWorkspaceSetting">
  >(
    `/v1/workspace-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

export async function updateTeamSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: RequestBody<"Settings_UpdateTeamSetting">
): Promise<SettingView> {
  const { data } = await appApi.put<JsonResponse<"Settings_UpdateTeamSetting">>(
    `/v1/team-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

export async function updateUserSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: RequestBody<"Settings_UpdateUserSetting">
): Promise<SettingView> {
  const { data } = await appApi.put<JsonResponse<"Settings_UpdateUserSetting">>(
    `/v1/user-settings-sections/${sectionId}/settings/${settingId}`,
    values
  );
  return data;
}

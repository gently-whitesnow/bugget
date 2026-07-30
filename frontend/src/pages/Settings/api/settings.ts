import type { Method } from "axios";
import { appApi } from "@/shared/api";
import type { paths } from "@/shared/api/generated/settings";
import type { Camelized } from "@/shared/lib/types";
import type { SettingView, SettingsSectionsResponse } from "./contracts";

/**
 * HTTP-клиенты модуля `settings`.
 *
 * Каждая ручка описана одной contract-bound записью: route template — литерал,
 * существующий в `paths` сгенерированного контракта (`satisfies keyof paths`), а
 * метод, сегменты пути, тело запроса и тело ответа выводятся из той же записи
 * `paths[route][method]`. Отдельного рукописного представления маршрута нет: сменили
 * в `specs/contracts/settings/openapi.yaml` путь, метод или форму тела — красным
 * становится сам вызов, а не только его тип.
 *
 * Запрос уходит тем же `appApi` с теми же интерсепторами: тела ответов приходят в
 * snake_case и перекладываются в camelCase (отсюда `Camelized<T>` на ответе), тело
 * PUT — голый массив строк, в нём переименовывать нечего. Сегменты пути конверсию не
 * проходят: их имена — часть публичного контракта (ADR-0009). Подстановка сегментов
 * дословная, как и была: URL на проводе не изменился.
 */

/** Маршруты модуля — литералы, существующие в контракте. */
export const settingsRoutes = {
  sections: "/v1/settings-sections",
  workspaceSetting:
    "/v1/workspace-settings-sections/{sectionId}/settings/{settingId}",
  teamSetting: "/v1/team-settings-sections/{sectionId}/settings/{settingId}",
  userSetting: "/v1/user-settings-sections/{sectionId}/settings/{settingId}",
} as const satisfies Record<string, keyof paths>;

export type SettingsRoute =
  (typeof settingsRoutes)[keyof typeof settingsRoutes];

/** Запись контракта описывает операцию, если у неё есть ответы. */
type ContractOperation = { responses: Record<number, unknown> };

/**
 * Методы, объявленные для маршрута в контракте. У остальных в `paths` стоит
 * `never`, поэтому обращение к ним не проходит проверку типов.
 */
export type SettingsMethod<R extends SettingsRoute> = {
  // `-?` обязателен: необъявленные методы в `paths` описаны как `put?: never`, и без
  // снятия optional в объединение просочился бы `undefined`.
  [M in keyof paths[R]]-?: paths[R][M] extends ContractOperation ? M : never;
}[keyof paths[R]];

type Operation<
  R extends SettingsRoute,
  M extends SettingsMethod<R>,
> = paths[R][Extract<M, keyof paths[R]>];

/** Сегменты пути операции; у маршрута без сегментов — пустой объект. */
type PathParams<R extends SettingsRoute, M extends SettingsMethod<R>> =
  Operation<R, M> extends { parameters: { path: infer P } }
    ? P
    : Record<string, never>;

/** Тело запроса операции; у операции без тела его передать нельзя. */
type RequestBody<R extends SettingsRoute, M extends SettingsMethod<R>> =
  Operation<R, M> extends {
    requestBody: { content: { "application/json": infer B } };
  }
    ? B
    : never;

/** Успешный ответ операции в том виде, в каком его отдаёт HTTP-слой фронта. */
type JsonResponse<R extends SettingsRoute, M extends SettingsMethod<R>> =
  Operation<R, M> extends {
    responses: { 200: { content: { "application/json": infer B } } };
  }
    ? Camelized<B>
    : never;

/** Три ручки обновления настройки отличаются только уровнем — и маршрутом. */
type UpdateSettingRoute =
  | typeof settingsRoutes.workspaceSetting
  | typeof settingsRoutes.teamSetting
  | typeof settingsRoutes.userSetting;

type SectionId = PathParams<UpdateSettingRoute, "put">["sectionId"];
type SettingId = PathParams<UpdateSettingRoute, "put">["settingId"];
type UpdateSettingBody = RequestBody<UpdateSettingRoute, "put">;

/**
 * Подставляет сегменты в route template. Значения не экранируются — так же, как
 * это делала прежняя строковая интерполяция; менять отправляемый URL нельзя.
 */
const buildPath = (
  route: SettingsRoute,
  params: Record<string, string>
): string =>
  route.replace(/\{(\w+)\}/g, (_, name: string) => {
    const value = params[name];
    if (value === undefined) {
      // Сегмента нет среди path-параметров операции — расхождение контракта с самим
      // собой. Молча отправить `undefined` в URL хуже, чем упасть здесь.
      throw new Error(`Не задан сегмент пути {${name}} для ${route}`);
    }
    return value;
  });

/** Единственная точка вызова: метод и URL берутся из записи контракта. */
const callContract = async <
  R extends SettingsRoute,
  // `& Method`: HTTP-верб уходит в axios, поэтому обязан быть верб из его набора.
  M extends SettingsMethod<R> & Method,
>(
  route: R,
  method: M,
  pathParams: PathParams<R, M> & Record<string, string>,
  body?: RequestBody<R, M>
): Promise<JsonResponse<R, M>> => {
  const { data } = await appApi.request<JsonResponse<R, M>>({
    method,
    url: buildPath(route, pathParams),
    data: body,
  });
  return data;
};

export async function fetchSettingsSections(): Promise<SettingsSectionsResponse> {
  return callContract(settingsRoutes.sections, "get", {});
}

export async function updateWorkspaceSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: UpdateSettingBody
): Promise<SettingView> {
  return callContract(
    settingsRoutes.workspaceSetting,
    "put",
    { sectionId, settingId },
    values
  );
}

export async function updateTeamSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: UpdateSettingBody
): Promise<SettingView> {
  return callContract(
    settingsRoutes.teamSetting,
    "put",
    { sectionId, settingId },
    values
  );
}

export async function updateUserSetting(
  sectionId: SectionId,
  settingId: SettingId,
  values: UpdateSettingBody
): Promise<SettingView> {
  return callContract(
    settingsRoutes.userSetting,
    "put",
    { sectionId, settingId },
    values
  );
}

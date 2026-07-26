/**
 * Провайдеры привязки аккаунта. Внешние (telegram/google/yandex) жили только в SaaS
 * и вместе с ним выпилены — остаётся внутренняя привязка Mattermost.
 */
export const externalProviders = [] as const;

export const internalProviders = ["mattermost"] as const;

export const showExternalProviders = externalProviders.length > 0;
export const showInternalProviders = internalProviders.length > 0;

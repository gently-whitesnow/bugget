export {
  appApi,
  parseAppContextFromPath,
  setAppContext,
  getAppContext,
  getAppWebSocketUrl,
  setSignalRConnectionId,
  getSignalRConnectionId,
} from "./instances";

export { buildQueryString } from "./buildQuery";
export type { QueryValue } from "./buildQuery";

export { createOperationRequest } from "./operation";
export type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "./operation";

// Операции модуля reports — единственная транспортная граница этого модуля
export * as reportsApi from "./reports";

// Канон `Int64String` с провода: неотрицательный 64-битный идентификатор и
// счётчик ходят строкой, потому что число здесь — double (shared.yaml).
export { isWireInt64, wireInt64ToBigInt, compareWireInt64 } from "./wireInt64";
export type { WireInt64 } from "./wireInt64";

export { parseApiError } from "./parseApiError";
export type { ApiError } from "./parseApiError";

export * from "./contracts";

export * as selfHostedApi from "./selfHosted";

// Имя `usersApi` принадлежит операциям, а не axios-инстансу: инстанс из индекса
// убран, чтобы адрес модуля собирался только здесь.
export * as usersApi from "./users";

// То же для `authorizationApi`: инстанс и хелпер `authorizationPath` убраны.
export * as authorizationApi from "./authorization";

export * as externalApi from "./external";

export * as settingsApi from "./settings";

export * as analyticsApi from "./analytics";
export type {
  AnalyticsSummary,
  AnalyticsReport,
  AnalyticsReportPhaseEntry,
  AnalyticsReportBugsByStatus,
  AnalyticsResponsible,
  AnalyticsResponsibleParticipatedReport,
  AnalyticsResponsibleCompletedReport,
  Period,
  PhaseName,
  ResponsibleOutcome,
  AvgPhaseDurationDays,
  PhaseTimeDistribution,
  TopRegressionReport,
  PhaseTrendWeekly,
} from "./analytics";

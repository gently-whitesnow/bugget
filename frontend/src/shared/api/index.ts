// API инстансы
export {
  authorizationApi,
  authorizationPath,
  appApi,
  parseAppContextFromPath,
  setAppContext,
  getAppContext,
  getAppWebSocketUrl,
  setSignalRConnectionId,
  getSignalRConnectionId,
} from "./instances";

// Сборка query по именам из контракта
export { buildQueryString } from "./buildQuery";
export type { QueryValue } from "./buildQuery";

// Граница «операция контракта → HTTP»
export { createOperationRequest } from "./operation";
export type {
  MethodsOf,
  OperationBody,
  OperationQuery,
  OperationResponse,
} from "./operation";

// Операции модуля reports — единственная транспортная граница этого модуля
export * as reportsApi from "./reports";

// Разбор ошибок API
export { parseApiError } from "./parseApiError";
export type { ApiError } from "./parseApiError";

// Контракты
export * from "./contracts";

// Self-hosted API
export * as selfHostedApi from "./selfHosted";

// Операции модуля users — единственная транспортная граница этого модуля.
// Имя `usersApi` теперь принадлежит им, а не axios-инстансу: инстанс из
// публичного индекса убран, чтобы адрес модуля собирался только здесь.
export * as usersApi from "./users";

// Analytics API helpers
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

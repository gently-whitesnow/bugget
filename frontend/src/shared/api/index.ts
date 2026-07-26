// API инстансы
export {
  authorizationApi,
  authorizationPath,
  usersApi,
  usersPath,
  usersPathWithContext,
  appApi,
  parseAppContextFromPath,
  setAppContext,
  getAppContext,
  getAppWebSocketUrl,
  setSignalRConnectionId,
  getSignalRConnectionId,
} from "./instances";

// Контракты
export * from "./contracts";

// Self-hosted API
export * as selfHostedApi from "./selfHosted";

// Users API helpers
export * from "./users";

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

/**
 * Контекст приложения для API вызовов
 * Содержит workspaceId и teamId для формирования путей
 */
export type AppContext = {
  workspaceId: number;
  teamId: number;
};

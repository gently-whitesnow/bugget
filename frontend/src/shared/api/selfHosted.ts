/**
 * Bootstrap self-hosted-контура: вступление в пространство и команду, чтение
 * стартового экрана и админские ручки команд. Транспорт живёт в операциях
 * модуля (`shared/api/users`) — здесь только имена, под которыми эти ручки зовёт
 * модель bootstrap.
 */
export {
  joinWorkspace,
  joinTeam,
  createTeam,
  updateTeam,
  deleteTeam,
  listWorkspacesContext as fetchWorkspacesContext,
} from "./users";

import { useUnit } from "effector-react";
import { useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  LayoutDashboard,
  Building2,
  UsersRound,
  Plus,
  Pencil,
  Trash2,
  ChevronDown,
  ChevronRight,
} from "lucide-react";
import { $authUserStore } from "@/entities/user";
import {
  $workspaces,
  $teamsMember,
  $workspacesMember,
  createTeamFx,
  deleteTeamFx,
  renameTeamFx,
} from "@/shared/model";
import { LogoutButton } from "./components/LogoutButton";
import {
  showDashboard,
  hideDashboard,
  $isDashboardVisible,
} from "@/entities/dashboard";
import { ActionDropdown, SidebarContainer } from "@/shared/ui";
import { useNotifications, notificationMessages } from "@/shared/model";
import { renameWorkspaceFx } from "@/entities/saas/workspace";

export const CustomLeftSidebar = () => {
  const { teamId: pageTeamId } = useParams<{ teamId: string }>();
  const location = useLocation();
  const navigate = useNavigate();

  const authUser = useUnit($authUserStore);
  const user = authUser;
  const workspaces = useUnit($workspaces);
  const teamsMember = useUnit($teamsMember);
  const workspacesMember = useUnit($workspacesMember);
  const isDashboardVisible = useUnit($isDashboardVisible);
  const { notifyError } = useNotifications();
  const isCreatingTeam = useUnit(createTeamFx.pending);
  const isRenamingTeam = useUnit(renameTeamFx.pending);
  const isDeletingTeam = useUnit(deleteTeamFx.pending);
  const sidebarActions = useUnit({
    createTeamFx,
    deleteTeamFx,
    renameTeamFx,
    renameWorkspaceFx,
    showDashboard,
    hideDashboard,
  });

  const [createFormWorkspaceId, setCreateFormWorkspaceId] = useState<
    string | number | null
  >(null);
  const [newTeamName, setNewTeamName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [expandedOtherTeams, setExpandedOtherTeams] = useState<
    Record<string, boolean>
  >({});

  const isTeamSelected = (teamId: string | number) => {
    const isTeamView =
      location.hash === "#team" ||
      (location.hash === "" && !isDashboardVisible);

    return String(teamId) === pageTeamId && isTeamView;
  };

  const normalizeTeamName = (name: string) => name.trim().toLowerCase();

  const getTeamPath = (teamId: string | number) => `/teams/${teamId}#team`;

  const hasUser = Boolean(user?.id);
  const memberTeamIds = new Set(
    teamsMember.map((member) => String(member.teamId))
  );
  const currentTeamId = pageTeamId;

  const userSettingsPath = currentTeamId
    ? `/teams/${currentTeamId}/settings?tab=user`
    : null;

  const startCreateTeam = (workspaceId: string | number) => {
    setCreateFormWorkspaceId(workspaceId);
    setNewTeamName("");
    setCreateError(null);
  };

  const cancelCreateTeam = () => {
    setCreateFormWorkspaceId(null);
    setNewTeamName("");
    setCreateError(null);
  };

  const handleCreateTeam = async (workspaceId: string | number) => {
    const name = newTeamName.trim();
    if (!name) return;

    const workspace = workspaces.find(
      (item) => String(item.id) === String(workspaceId)
    );
    const existingNames = workspace?.teams?.map((team) =>
      normalizeTeamName(team.name)
    );
    if (existingNames?.includes(normalizeTeamName(name))) {
      setCreateError("Команда с таким названием уже существует");
      return;
    }

    try {
      const teamId = await sidebarActions.createTeamFx({ workspaceId, name });
      sidebarActions.hideDashboard();
      cancelCreateTeam();
      navigate(getTeamPath(teamId));
    } catch (err: unknown) {
      console.error("Failed to create team:", err);
      const axiosError = err as {
        response?: { data?: { reason?: string } };
      };
      const reason = axiosError?.response?.data?.reason;
      notifyError(
        "Не удалось создать команду",
        reason ?? notificationMessages.errorRetry,
        {
          dedupeKey: "sidebar-create-team-failed",
        }
      );
    }
  };

  const handleRenameTeam = async (
    workspaceId: string | number,
    teamId: string | number,
    currentName: string
  ) => {
    const nextName = prompt("Новое название команды", currentName);
    if (nextName === null) return;
    const name = nextName.trim();
    if (!name || name === currentName) return;

    const workspace = workspaces.find(
      (item) => String(item.id) === String(workspaceId)
    );
    const isDuplicate = workspace?.teams?.some(
      (team) =>
        String(team.id) !== String(teamId) &&
        normalizeTeamName(team.name) === normalizeTeamName(name)
    );
    if (isDuplicate) {
      notifyError("Команда с таким названием уже существует", undefined, {
        dedupeKey: "sidebar-rename-team-duplicate",
      });
      return;
    }

    try {
      await sidebarActions.renameTeamFx({ workspaceId, teamId, name });
    } catch (err) {
      console.error("Failed to rename team:", err);
      notifyError(
        "Не удалось переименовать команду",
        notificationMessages.errorRetry,
        {
          dedupeKey: "sidebar-rename-team-failed",
        }
      );
    }
  };

  const handleDeleteTeam = async (
    workspaceId: string | number,
    teamId: string | number,
    teamName: string
  ) => {
    const confirmed = confirm(
      `⚠️ Удалить команду "${teamName}"? Это действие необратимо.`
    );
    if (!confirmed) return;

    try {
      await sidebarActions.deleteTeamFx({ workspaceId, teamId });

      if (pageTeamId === String(teamId)) {
        navigate("/", { replace: true });
      }
    } catch (err) {
      console.error("Failed to delete team:", err);
      notifyError(
        "Не удалось удалить команду",
        notificationMessages.errorRetry,
        {
          dedupeKey: "sidebar-delete-team-failed",
        }
      );
    }
  };

  const handleRenameWorkspace = async (
    workspaceId: string | number,
    currentName: string
  ) => {
    const nextName = prompt("Новое название рабочей области", currentName);
    if (nextName === null) return;
    const name = nextName.trim();
    if (!name || name === currentName) return;

    try {
      await sidebarActions.renameWorkspaceFx({ workspaceId, name });
    } catch (err) {
      console.error("Failed to rename workspace:", err);
      notifyError(
        "Не удалось переименовать рабочую область",
        notificationMessages.errorRetry,
        { dedupeKey: "sidebar-rename-workspace-failed" }
      );
    }
  };

  const toggleOtherTeams = (workspaceId: string | number) => {
    const workspaceKey = String(workspaceId);
    setExpandedOtherTeams((current) => ({
      ...current,
      [workspaceKey]: !current[workspaceKey],
    }));
  };

  return (
    <SidebarContainer side="left">
      <div className="flex flex-col gap-4 h-full">
        {/* Информация о пользователе */}
        {hasUser && (
          <button
            type="button"
            className="flex items-center gap-2 text-left rounded-md px-2 py-1 hover:bg-base-200 transition-colors cursor-pointer disabled:opacity-70 disabled:cursor-default"
            onClick={() => {
              if (!userSettingsPath) return;
              navigate(userSettingsPath);
            }}
            disabled={!userSettingsPath}
          >
            {user.imageUrl ? (
              <img
                src={user.imageUrl}
                alt={user.name ?? "Пользователь"}
                className="w-6 h-6 rounded-full object-cover"
              />
            ) : (
              <div className="w-6 h-6 rounded-full bg-primary/20 flex items-center justify-center">
                <span className="text-lg font-semibold text-primary">
                  {(user.name ? user.name.charAt(0) : "?").toUpperCase()}
                </span>
              </div>
            )}
            <div className="flex flex-col">
              <span className="text-sm font-medium text-base-content inline-flex items-center gap-1">
                {user.name ?? "Пользователь"}
              </span>
            </div>
          </button>
        )}

        {/* Кнопка дашборд - всегда видна */}
        <button
          type="button"
          onClick={() => {
            // убираем #... полностью, сохраняя pathname + search
            navigate(
              {
                pathname: location.pathname,
                search: location.search,
                hash: "",
              },
              { replace: true }
            );
            sidebarActions.showDashboard();
          }}
          aria-current={isDashboardVisible ? "page" : undefined}
          className={`flex w-full cursor-pointer items-center gap-2 rounded-md border px-2 py-1.5 text-left text-sm transition-colors ${
            isDashboardVisible
              ? "bg-base-200 text-base-content border-base-content/20 font-medium"
              : "text-base-content/70 border-transparent hover:bg-base-200 hover:text-base-content"
          }`}
        >
          <LayoutDashboard
            className={`h-4 w-4 shrink-0 ${
              isDashboardVisible
                ? "text-base-content/100"
                : "text-base-content/80"
            }`}
          />
          <span className="truncate">Дашборд</span>
        </button>

        {/* Рабочие пространства */}
        <div className="flex flex-col gap-2 flex-1 overflow-y-auto">
          <h3 className="text-xs uppercase tracking-wider flex items-center gap-2">
            Рабочие пространства
          </h3>

          {workspaces.length > 0 ? (
            <div className="flex flex-col gap-3">
              {workspaces.map((workspace) => {
                const workspaceKey = String(workspace.id);
                const workspaceTeams = workspace.teams ?? [];
                const workspaceMemberTeams = workspaceTeams.filter((team) =>
                  memberTeamIds.has(String(team.id))
                );
                const workspaceOtherTeams = workspaceTeams.filter(
                  (team) => !memberTeamIds.has(String(team.id))
                );
                const hasActiveOtherTeam = workspaceOtherTeams.some((team) =>
                  isTeamSelected(team.id)
                );
                const isOtherTeamsExpanded =
                  hasActiveOtherTeam ||
                  Boolean(expandedOtherTeams[workspaceKey]);
                const isWorkspaceAdmin = workspacesMember.some(
                  (member) =>
                    String(member.workspaceId) === String(workspace.id) &&
                    member.role === "admin"
                );

                return (
                  <div key={workspace.id} className="flex flex-col gap-1">
                    <div className="relative flex min-w-0 items-center pr-7">
                      <div className="flex min-w-0 flex-1 items-center gap-1.5 text-sm font-bold">
                        <Building2 className="h-4 w-4 shrink-0" />
                        <span
                          className="block truncate whitespace-nowrap"
                          title={workspace.name}
                        >
                          {workspace.name}
                        </span>
                      </div>

                      {isWorkspaceAdmin && (
                        <div className="absolute right-0 top-1/2 -translate-y-1/2">
                          <ActionDropdown
                            items={[
                              {
                                icon: <Plus className="w-4 h-4" />,
                                label: "Создать команду",
                                onClick: () => startCreateTeam(workspace.id),
                              },
                              {
                                icon: <Pencil className="w-4 h-4" />,
                                label: "Переименовать",
                                onClick: () =>
                                  handleRenameWorkspace(
                                    workspace.id,
                                    workspace.name
                                  ),
                              },
                            ]}
                            triggerClassName="btn btn-ghost btn-xs btn-square h-6 min-h-6 w-6 p-0 text-base-content/70 hover:text-base-content"
                            menuPosition="bottom-left"
                          />
                        </div>
                      )}
                    </div>

                    {String(createFormWorkspaceId) === String(workspace.id) &&
                      isWorkspaceAdmin && (
                        <div className="ml-3 mt-2 border-l-2 border-base-300 rounded-md p-2">
                          <input
                            type="text"
                            placeholder="Название команды"
                            className="input input-bordered input-sm w-full"
                            value={newTeamName}
                            onChange={(e) => {
                              setNewTeamName(e.target.value);
                              setCreateError(null);
                            }}
                            disabled={
                              isCreatingTeam || isRenamingTeam || isDeletingTeam
                            }
                          />
                          {createError && (
                            <div className="text-xs text-error mt-2">
                              {createError}
                            </div>
                          )}
                          <div className="flex gap-2 mt-2">
                            <button
                              className="btn btn-primary btn-sm flex-1"
                              onClick={() => handleCreateTeam(workspace.id)}
                              disabled={
                                isCreatingTeam ||
                                !newTeamName.trim() ||
                                Boolean(
                                  workspace.teams?.some(
                                    (team) =>
                                      normalizeTeamName(team.name) ===
                                      normalizeTeamName(newTeamName)
                                  )
                                ) ||
                                isRenamingTeam ||
                                isDeletingTeam
                              }
                            >
                              {isCreatingTeam ? (
                                <>
                                  <span className="loading loading-spinner loading-xs"></span>
                                  Создание...
                                </>
                              ) : (
                                "Создать"
                              )}
                            </button>
                            <button
                              className="btn btn-ghost btn-sm"
                              onClick={cancelCreateTeam}
                              disabled={
                                isCreatingTeam ||
                                isRenamingTeam ||
                                isDeletingTeam
                              }
                            >
                              Отмена
                            </button>
                          </div>
                        </div>
                      )}

                    {workspaceMemberTeams.length > 0 && (
                      <div
                        className={`flex flex-col gap-1 ml-3 mt-1 border-l-2 border-base-300 rounded-md p-1 pl-2`}
                      >
                        {workspaceOtherTeams.length > 0 && (
                          <div className="px-2 py-1 text-[11px] uppercase tracking-wide text-base-content/50">
                            Мои команды
                          </div>
                        )}
                        {workspaceMemberTeams.map((team) => (
                          <div
                            key={team.id}
                            className="group flex items-center gap-1"
                          >
                            <Link
                              to={getTeamPath(team.id)}
                              className={`flex-1 text-xs px-2 py-1.5 rounded-md flex items-center gap-2 border transition-colors ${isTeamSelected(team.id) ? "bg-base-200 text-base-content border-base-content/20 font-medium" : "text-base-content/70 border-transparent hover:text-base-content hover:bg-base-200"}`}
                              onClick={() => {
                                sidebarActions.hideDashboard();
                              }}
                            >
                              <UsersRound
                                className={`w-3 h-3 ${isTeamSelected(team.id) ? "text-base-content/80" : "text-base-content/40"}`}
                              />
                              {team.name}
                            </Link>

                            {isWorkspaceAdmin && (
                              <ActionDropdown
                                items={[
                                  {
                                    icon: <Pencil className="w-4 h-4" />,
                                    label: "Переименовать",
                                    onClick: () =>
                                      handleRenameTeam(
                                        workspace.id,
                                        team.id,
                                        team.name
                                      ),
                                  },
                                  {
                                    icon: <Trash2 className="w-4 h-4" />,
                                    label: "Удалить",
                                    className:
                                      "text-error hover:bg-error/10 focus:bg-error/10",
                                    onClick: () =>
                                      handleDeleteTeam(
                                        workspace.id,
                                        team.id,
                                        team.name
                                      ),
                                  },
                                ]}
                                triggerClassName="btn btn-ghost btn-xs p-1 text-base-content/70 hover:text-base-content opacity-0 group-hover:opacity-100"
                                menuPosition="bottom-left"
                              />
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {workspaceOtherTeams.length > 0 && (
                      <div className="flex flex-col gap-1 ml-3 mt-1 border-l-2 border-base-300 rounded-md p-1 pl-2">
                        <button
                          type="button"
                          onClick={() => toggleOtherTeams(workspace.id)}
                          className="w-full flex items-center justify-between px-2 py-1 text-[11px] uppercase tracking-wide text-base-content/60 hover:text-base-content rounded-md hover:bg-base-200 transition-colors"
                        >
                          <span>
                            Остальные команды ({workspaceOtherTeams.length})
                          </span>
                          {isOtherTeamsExpanded ? (
                            <ChevronDown className="w-3 h-3" />
                          ) : (
                            <ChevronRight className="w-3 h-3" />
                          )}
                        </button>

                        {isOtherTeamsExpanded &&
                          workspaceOtherTeams.map((team) => (
                            <div
                              key={team.id}
                              className="group flex items-center gap-1"
                            >
                              <Link
                                to={getTeamPath(team.id)}
                                className={`flex-1 text-xs px-2 py-1.5 rounded-md flex items-center gap-2 border transition-colors ${isTeamSelected(team.id) ? "bg-base-200 text-base-content border-base-content/20 font-medium" : "text-base-content/60 border-transparent hover:text-base-content hover:bg-base-200"}`}
                                onClick={() => {
                                  sidebarActions.hideDashboard();
                                }}
                              >
                                <UsersRound
                                  className={`w-3 h-3 ${isTeamSelected(team.id) ? "text-base-content/80" : "text-base-content/40"}`}
                                />
                                {team.name}
                              </Link>

                              {isWorkspaceAdmin && (
                                <ActionDropdown
                                  items={[
                                    {
                                      icon: <Pencil className="w-4 h-4" />,
                                      label: "Переименовать",
                                      onClick: () =>
                                        handleRenameTeam(
                                          workspace.id,
                                          team.id,
                                          team.name
                                        ),
                                    },
                                    {
                                      icon: <Trash2 className="w-4 h-4" />,
                                      label: "Удалить",
                                      className:
                                        "text-error hover:bg-error/10 focus:bg-error/10",
                                      onClick: () =>
                                        handleDeleteTeam(
                                          workspace.id,
                                          team.id,
                                          team.name
                                        ),
                                    },
                                  ]}
                                  triggerClassName="btn btn-ghost btn-xs p-1 text-base-content/70 hover:text-base-content opacity-0 group-hover:opacity-100"
                                  menuPosition="bottom-left"
                                />
                              )}
                            </div>
                          ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="text-xs text-base-content/50">
              Нет рабочих пространств
            </p>
          )}
        </div>

        {/* Кнопка выхода внизу */}
        <LogoutButton />
      </div>
    </SidebarContainer>
  );
};

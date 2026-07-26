import { useUnit } from "effector-react";
import { useState } from "react";
import {
  $bootstrapState,
  $workspacesMember,
  fetchBootstrapFx,
  joinTeamFx,
  createTeamFx,
} from "@/shared/model";
import type { TeamResponse } from "@/shared/api";
import { useSelfHostedAutoJoin } from "@/shared/lib/selfHostedAutoJoin";
import { BootstrapStatus } from "@/shared/config";

export const TeamSelect = () => {
  const [
    bootstrapState,
    fetchPending,
    joinPending,
    createPending,
    members,
    joinTeam,
    createTeam,
    runFetchBootstrap,
  ] = useUnit([
    $bootstrapState,
    fetchBootstrapFx.pending,
    joinTeamFx.pending,
    createTeamFx.pending,
    $workspacesMember,
    joinTeamFx,
    createTeamFx,
    fetchBootstrapFx,
  ]);
  const { isAutoJoining } = useSelfHostedAutoJoin();

  const [error, setError] = useState<string | null>(null);
  const [teamName, setTeamName] = useState("");
  const [showCreateForm, setShowCreateForm] = useState(false);

  const isPending = joinPending || createPending;

  // Данные из bootstrap state
  const workspace =
    bootstrapState.status !== BootstrapStatus.NO_WORKSPACE
      ? bootstrapState.workspace
      : null;
  const teams =
    bootstrapState.status === BootstrapStatus.NO_TEAM
      ? bootstrapState.availableTeams
      : [];
  const workspaceId = workspace?.id ?? 1;

  const workspaceRole = workspace
    ? members.find(
        (member) => String(member.workspaceId) === String(workspace.id)
      )?.role
    : undefined;
  const userCanCreateTeam = workspaceRole === "admin";

  const handleJoinTeam = async (team: TeamResponse) => {
    setError(null);
    try {
      // После успеха автоматически перезагрузятся workspaces
      // и произойдёт редирект через изменение bootstrapState
      await joinTeam({ workspaceId, teamId: team.id });
    } catch (err) {
      console.error("Failed to join team:", err);
      setError("Не удалось присоединиться к команде");
    }
  };

  const normalizeTeamName = (name: string) => name.trim().toLowerCase();

  const handleCreateTeam = async () => {
    if (!teamName.trim()) return;

    setError(null);
    const duplicateName = teams.some(
      (team) => normalizeTeamName(team.name) === normalizeTeamName(teamName)
    );
    if (duplicateName) {
      setError("Команда с таким названием уже существует");
      return;
    }
    try {
      // После успеха автоматически перезагрузятся workspaces
      // и произойдёт редирект через изменение bootstrapState
      await createTeam({ workspaceId, name: teamName.trim() });
    } catch (err) {
      console.error("Failed to create team:", err);
      setError("Не удалось создать команду");
    }
  };

  if (isAutoJoining) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="loading loading-spinner loading-lg"></div>
      </div>
    );
  }

  const guestTeamId = "1";
  const guestTeam = teams.find((t) => String(t.id) === guestTeamId);
  const otherTeams = teams.filter((t) => String(t.id) !== guestTeamId);
  const useGrid = otherTeams.length >= 10;

  // Нет команд и пользователь не может создавать
  if (teams.length === 0 && !userCanCreateTeam) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-base-100">
        <div className="max-w-md w-full mx-auto px-4">
          <div className="text-center mb-8">
            <h1 className="text-3xl font-bold mb-2">Ожидание</h1>
            <p className="text-base-content/70">
              Команды ещё не созданы. Ожидайте, пока администратор создаст
              команду.
            </p>
          </div>

          <div className="card bg-base-200 shadow-xl">
            <div className="card-body text-center">
              <div className="loading loading-dots loading-lg mx-auto"></div>
              <p className="text-sm opacity-70 mt-4">
                Обновите страницу после создания команды администратором
              </p>
              <button
                onClick={() => runFetchBootstrap()}
                className="btn btn-outline btn-sm mt-4"
                disabled={fetchPending}
              >
                Обновить
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-base-100">
      <div
        className={`w-full mx-auto px-4 ${useGrid ? "max-w-2xl" : "max-w-md"}`}
      >
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold mb-2">Выберите команду</h1>
          <p className="text-base-content/70">
            {teams.length > 0
              ? "Присоединитесь к существующей команде или создайте новую"
              : "Создайте первую команду для начала работы"}
          </p>
        </div>

        {error && (
          <div className="alert alert-error mb-6">
            <span>{error}</span>
          </div>
        )}

        <div className="card bg-base-200 shadow-xl">
          <div className="card-body space-y-4">
            {/* Команда "Гость" — всегда вверху */}
            {guestTeam && (
              <div>
                <button
                  onClick={() => handleJoinTeam(guestTeam)}
                  className="btn btn-outline w-full"
                  disabled={isPending}
                >
                  {joinPending ? (
                    <>
                      <span className="loading loading-spinner loading-sm"></span>
                      Присоединяемся...
                    </>
                  ) : (
                    guestTeam.name
                  )}
                </button>
              </div>
            )}

            {/* Разделитель между Гость и остальными */}
            {guestTeam && otherTeams.length > 0 && (
              <div className="divider">или</div>
            )}

            {/* Остальные команды */}
            {otherTeams.length > 0 && (
              <div>
                <h3 className="font-medium text-sm opacity-70 mb-2">
                  Существующие команды
                </h3>
                <div
                  className={
                    useGrid
                      ? "responsive-card-grid [--responsive-card-min:12rem]"
                      : "flex flex-col gap-2"
                  }
                >
                  {otherTeams.map((team) => (
                    <button
                      key={team.id}
                      onClick={() => handleJoinTeam(team)}
                      className="btn btn-primary w-full justify-start"
                      disabled={isPending}
                    >
                      {joinPending ? (
                        <>
                          <span className="loading loading-spinner loading-sm"></span>
                          Присоединяемся...
                        </>
                      ) : (
                        team.name
                      )}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Создание команды */}
            {userCanCreateTeam && (
              <>
                {teams.length > 0 && <div className="divider">или</div>}

                {showCreateForm ? (
                  <div className="space-y-3">
                    <input
                      type="text"
                      placeholder="Название команды"
                      className="input input-bordered w-full"
                      value={teamName}
                      onChange={(e) => {
                        setTeamName(e.target.value);
                        setError(null);
                      }}
                      disabled={isPending}
                    />
                    <div className="flex gap-2">
                      <button
                        onClick={handleCreateTeam}
                        className="btn btn-primary flex-1"
                        disabled={
                          isPending ||
                          !teamName.trim() ||
                          teams.some(
                            (team) =>
                              normalizeTeamName(team.name) ===
                              normalizeTeamName(teamName)
                          )
                        }
                      >
                        {createPending ? (
                          <>
                            <span className="loading loading-spinner loading-sm"></span>
                            Создание...
                          </>
                        ) : (
                          "Создать"
                        )}
                      </button>
                      <button
                        onClick={() => {
                          setShowCreateForm(false);
                          setTeamName("");
                        }}
                        className="btn btn-ghost"
                        disabled={isPending}
                      >
                        Отмена
                      </button>
                    </div>
                  </div>
                ) : (
                  <button
                    onClick={() => setShowCreateForm(true)}
                    className="btn btn-primary w-full"
                    disabled={isPending}
                  >
                    Создать команду
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

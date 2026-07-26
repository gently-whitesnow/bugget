import { useUnit } from "effector-react";
import { useEffect, useRef, useState } from "react";
import { joinWorkspaceFx } from "@/shared/model";
import { useSelfHostedAutoJoin } from "@/shared/lib/selfHostedAutoJoin";

export const WorkspaceJoin = () => {
  const [joinWorkspace, joinPending] = useUnit([
    joinWorkspaceFx,
    joinWorkspaceFx.pending,
  ]);
  const [error, setError] = useState<string | null>(null);
  const { isAutoJoining, autoJoinParams } = useSelfHostedAutoJoin();
  const autoJoinAttempted = useRef(false);

  useEffect(() => {
    if (autoJoinParams || autoJoinAttempted.current) return;
    autoJoinAttempted.current = true;
    joinWorkspace(1).catch(() => {
      setError("Не удалось присоединиться к рабочей области");
    });
  }, [autoJoinParams, joinWorkspace]);

  const handleJoinWorkspace = async () => {
    setError(null);
    try {
      await joinWorkspace(1);
    } catch {
      setError("Не удалось присоединиться к рабочей области");
    }
  };

  if (!error && (isAutoJoining || joinPending)) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="loading loading-spinner loading-lg"></div>
      </div>
    );
  }

  if (!error) {
    return null;
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-base-100">
      <div className="max-w-md w-full mx-auto px-4">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold mb-2">Добро пожаловать!</h1>
          <p className="text-base-content/70">
            Присоединитесь к рабочей области для начала работы
          </p>
        </div>

        <div className="alert alert-error mb-6">
          <span>{error}</span>
        </div>

        <div className="card bg-base-200 shadow-xl">
          <div className="card-body">
            <button
              onClick={handleJoinWorkspace}
              className="btn btn-primary w-full"
              disabled={joinPending}
            >
              {joinPending ? (
                <>
                  <span className="loading loading-spinner loading-sm"></span>
                  Присоединяемся...
                </>
              ) : (
                "Повторить"
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

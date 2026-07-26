import { useEffect } from "react";
import { useNavigate } from "react-router";
import { useUnit } from "effector-react";
import { createStore, createEvent } from "effector";

import {
  $bootstrapState,
  fetchBootstrapFx,
  joinTeamFx,
  joinWorkspaceFx,
} from "@/shared/model";
import {
  buildAuthRedirectUrl,
  clearLoginNextFromSession,
} from "@/shared/lib/auth";
import { BootstrapStatus } from "@/shared/config";

type AutoJoinParams = {
  workspaceId: number;
  teamId: number;
  reportId: string | null;
};

const autoJoinSessionKey = "autoJoinParams";

const toPositiveId = (value: unknown): number | null => {
  const numericValue = Number(value);
  if (!Number.isInteger(numericValue) || numericValue <= 0) return null;
  return numericValue;
};

const createAutoJoinParams = ({
  workspaceId,
  teamId,
  reportId,
}: {
  workspaceId: unknown;
  teamId: unknown;
  reportId: unknown;
}): AutoJoinParams | null => {
  const normalizedWorkspaceId = toPositiveId(workspaceId);
  const normalizedTeamId = toPositiveId(teamId);
  const normalizedReportId =
    typeof reportId === "string" ? reportId.trim() || null : null;

  if (!normalizedWorkspaceId || !normalizedTeamId) return null;

  return {
    workspaceId: normalizedWorkspaceId,
    teamId: normalizedTeamId,
    reportId: normalizedReportId,
  };
};

const parseAutoJoinParamsFromSearch = (
  search: string
): AutoJoinParams | null => {
  if (!search) return null;
  const params = new URLSearchParams(search);
  return createAutoJoinParams({
    workspaceId: params.get("workspaceId"),
    teamId: params.get("teamId"),
    reportId: params.get("reportId"),
  });
};

const parseAutoJoinParamsFromSession = (
  serialized: string
): AutoJoinParams | null => {
  try {
    const parsed = JSON.parse(serialized) as {
      workspaceId?: unknown;
      teamId?: unknown;
      reportId?: unknown;
    };
    return createAutoJoinParams({
      workspaceId: parsed.workspaceId,
      teamId: parsed.teamId,
      reportId: parsed.reportId,
    });
  } catch {
    return null;
  }
};

const persistAutoJoinParamsToSession = (params: AutoJoinParams): void => {
  sessionStorage.setItem(autoJoinSessionKey, JSON.stringify(params));
};

const readAutoJoinParamsFromSession = (): AutoJoinParams | null => {
  const serialized = sessionStorage.getItem(autoJoinSessionKey);
  if (!serialized) return null;
  return parseAutoJoinParamsFromSession(serialized);
};

const clearAutoJoinSession = (): void => {
  sessionStorage.removeItem(autoJoinSessionKey);
  clearLoginNextFromSession();
};

const getErrorStatus = (error: unknown): number | null => {
  if (error && typeof error === "object" && "response" in error) {
    const response = (error as { response?: { status?: number } }).response;
    return response?.status ?? null;
  }
  return null;
};

const buildTargetPath = (teamId: number, reportId: string | null): string => {
  if (!reportId) return `/teams/${teamId}`;

  const encodedReportId = encodeURIComponent(reportId);
  return `/teams/${teamId}/reports/${encodedReportId}?reportId=${encodedReportId}`;
};

const buildLoginRedirectPath = ({
  workspaceId,
  teamId,
  reportId,
}: AutoJoinParams): string => {
  const searchParams = new URLSearchParams({
    workspaceId: String(workspaceId),
    teamId: String(teamId),
  });
  if (reportId) {
    searchParams.set("reportId", reportId);
  }

  const next = `/?${searchParams.toString()}`;
  return (
    buildAuthRedirectUrl(next) ?? `/login?next=${encodeURIComponent(next)}`
  );
};

// Effector store survives component remounts.
// Session backup survives full-page auth redirects.
const getInitialParams = (): AutoJoinParams | null => {
  const fromUrl = parseAutoJoinParamsFromSearch(window.location.search);
  if (fromUrl) {
    persistAutoJoinParamsToSession(fromUrl);
    return fromUrl;
  }

  return readAutoJoinParamsFromSession();
};

const clearAutoJoinParams = createEvent();
clearAutoJoinParams.watch(() => {
  clearAutoJoinSession();
});

export const $autoJoinParamsStore = createStore<AutoJoinParams | null>(
  getInitialParams()
).reset(clearAutoJoinParams);

// Module-level flag to prevent concurrent run() across hook instances
let globalRunning = false;

export const useSelfHostedAutoJoin = () => {
  const [
    bootstrapState,
    bootstrapPending,
    autoJoinParams,
    clearAutoJoinParamsUnit,
    joinWorkspace,
    joinTeam,
  ] = useUnit([
    $bootstrapState,
    fetchBootstrapFx.pending,
    $autoJoinParamsStore,
    clearAutoJoinParams,
    joinWorkspaceFx,
    joinTeamFx,
  ]);
  const navigate = useNavigate();

  useEffect(() => {
    if (!autoJoinParams || bootstrapPending || globalRunning) {
      return;
    }

    const { workspaceId, teamId, reportId } = autoJoinParams;
    const targetPath = buildTargetPath(teamId, reportId);
    const isAlreadyMember =
      bootstrapState.status === BootstrapStatus.READY &&
      bootstrapState.memberTeams.some(
        (team) => String(team.teamId) === String(teamId)
      );

    if (isAlreadyMember) {
      clearAutoJoinParamsUnit();
      navigate(targetPath, { replace: true });
      return;
    }

    const run = async () => {
      globalRunning = true;
      try {
        if (bootstrapState.status === BootstrapStatus.NO_WORKSPACE) {
          await joinWorkspace(workspaceId);
        }

        await joinTeam({ workspaceId, teamId });
        clearAutoJoinParamsUnit();
        navigate(targetPath, { replace: true });
      } catch (error) {
        const status = getErrorStatus(error);
        if (status === 401) {
          clearAutoJoinParamsUnit();
          navigate(buildLoginRedirectPath({ workspaceId, teamId, reportId }), {
            replace: true,
          });
          return;
        }

        // Don't navigate away — keep params in store so next bootstrap
        // phase can retry. Only clear on auth errors.
      } finally {
        globalRunning = false;
      }
    };

    void run();
  }, [
    autoJoinParams,
    bootstrapPending,
    bootstrapState,
    clearAutoJoinParamsUnit,
    joinTeam,
    joinWorkspace,
    navigate,
  ]);

  return {
    autoJoinParams,
    isAutoJoining:
      Boolean(autoJoinParams) && (bootstrapPending || globalRunning),
  };
};

import { useEffect } from "react";
import {
  readLoginNextFromSession,
  saveLoginNextToSession,
} from "@/shared/lib/auth";

type AutoJoinQueryParams = {
  workspaceId: string | null;
  teamId: string | null;
  reportId: string | null;
};

const normalizePositiveId = (value: string | null): string | null => {
  if (!value) return null;
  const numericValue = Number(value);
  if (!Number.isInteger(numericValue) || numericValue <= 0) return null;
  return String(numericValue);
};

const extractAutoJoinParams = (
  searchParams: URLSearchParams
): AutoJoinQueryParams => {
  return {
    workspaceId: normalizePositiveId(searchParams.get("workspaceId")),
    teamId: normalizePositiveId(searchParams.get("teamId")),
    reportId: searchParams.get("reportId")?.trim() || null,
  };
};

const mergeMissingAutoJoinParams = (
  targetParams: URLSearchParams,
  sourceParams: AutoJoinQueryParams
) => {
  if (sourceParams.workspaceId && !targetParams.has("workspaceId")) {
    targetParams.set("workspaceId", sourceParams.workspaceId);
  }
  if (sourceParams.teamId && !targetParams.has("teamId")) {
    targetParams.set("teamId", sourceParams.teamId);
  }
  if (sourceParams.reportId && !targetParams.has("reportId")) {
    targetParams.set("reportId", sourceParams.reportId);
  }
};

const hasAnyAutoJoinParam = (params: AutoJoinQueryParams): boolean => {
  return Boolean(params.workspaceId || params.teamId || params.reportId);
};

const resolveNextFromSearch = (
  searchParams: URLSearchParams
): string | null => {
  const nextFromUrl = searchParams.get("next")?.trim() || null;
  const topLevelAutoJoinParams = extractAutoJoinParams(searchParams);

  // Handle malformed links like:
  // /login?next=/?workspaceId=1&teamId=2&reportId=17
  if (!nextFromUrl) {
    if (!topLevelAutoJoinParams.workspaceId || !topLevelAutoJoinParams.teamId) {
      return null;
    }

    const normalizedParams = new URLSearchParams();
    mergeMissingAutoJoinParams(normalizedParams, topLevelAutoJoinParams);
    return `/?${normalizedParams.toString()}`;
  }

  if (!hasAnyAutoJoinParam(topLevelAutoJoinParams)) {
    return nextFromUrl;
  }

  try {
    const parsedNext = new URL(nextFromUrl, window.location.origin);
    if (parsedNext.pathname && parsedNext.pathname !== "/") {
      return nextFromUrl;
    }

    const mergedParams = new URLSearchParams(parsedNext.search);
    mergeMissingAutoJoinParams(mergedParams, topLevelAutoJoinParams);

    const mergedSearch = mergedParams.toString();
    const pathname = parsedNext.pathname || "/";
    return `${pathname}${mergedSearch ? `?${mergedSearch}` : ""}${parsedNext.hash}`;
  } catch {
    return nextFromUrl;
  }
};

export const useLoginNext = (searchParams: URLSearchParams): string => {
  const nextFromUrl = resolveNextFromSearch(searchParams);

  useEffect(() => {
    if (!nextFromUrl) return;
    saveLoginNextToSession(nextFromUrl);
  }, [nextFromUrl]);

  return nextFromUrl || readLoginNextFromSession() || "/";
};

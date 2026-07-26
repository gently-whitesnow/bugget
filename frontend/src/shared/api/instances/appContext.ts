export type AppContext = {
  workspaceId: number | null;
  teamId: number | null;
};

/**
 * Путь вида /teams/:teamId/... — workspace в self-hosted всегда один (id = 1).
 */
export const parseAppContextFromPath = (pathname: string): AppContext => {
  const teamMatch = pathname.match(/\/teams\/(\d+)/);
  const teamId = teamMatch ? Number(teamMatch[1]) : null;

  return { workspaceId: teamId ? 1 : null, teamId };
};

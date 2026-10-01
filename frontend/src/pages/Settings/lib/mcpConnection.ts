import { getAppContext, parseAppContextFromPath } from "@/shared/api";

/** Плейсхолдер в сниппетах, пока значение токена ещё не показано. */
export const MCP_TOKEN_PLACEHOLDER = "bgt_pat_…";

export type McpAppContext = {
  workspaceId: string | number;
  teamId: string | number;
};

/**
 * Scope как у PAT: контекст приложения, иначе путь `/teams/:teamId`
 * (self-hosted: workspace всегда 1).
 */
export const resolveMcpAppContext = (
  pathname: string = typeof window !== "undefined"
    ? window.location.pathname
    : ""
): McpAppContext | null => {
  const fromStore = getAppContext();
  const fromStoreContext = toMcpAppContext(
    fromStore.workspaceId,
    fromStore.teamId
  );
  if (fromStoreContext !== null) return fromStoreContext;

  const fromPath = parseAppContextFromPath(pathname);
  return toMcpAppContext(fromPath.workspaceId, fromPath.teamId);
};

export const buildMcpEndpointUrl = (
  origin: string = typeof window !== "undefined" ? window.location.origin : "",
  pathname?: string
): string | null => {
  const context = resolveMcpAppContext(pathname);
  if (context === null) return null;

  return `${origin}/api/app/workspaces/${context.workspaceId}/teams/${context.teamId}/v1/mcp`;
};

/** Готовый фрагмент `~/.cursor/mcp.json` с корнем `mcpServers`. */
export const buildCursorMcpSnippet = (
  url: string,
  token: string = MCP_TOKEN_PLACEHOLDER
): string =>
  JSON.stringify(
    {
      mcpServers: {
        bugget: {
          url,
          headers: {
            Authorization: `Bearer ${token}`,
          },
        },
      },
    },
    null,
    2
  );

/** Фрагмент для Claude Code; без `type: "http"` клиент ждёт stdio. */
export const buildClaudeCodeMcpSnippet = (
  url: string,
  token: string = MCP_TOKEN_PLACEHOLDER
): string =>
  JSON.stringify(
    {
      mcpServers: {
        bugget: {
          type: "http",
          url,
          headers: {
            Authorization: `Bearer ${token}`,
          },
        },
      },
    },
    null,
    2
  );

/** Сниппет для Codex; токен — в env (`export BUGGET_PAT='…'`). */
export const buildCodexMcpSnippet = (url: string): string =>
  [
    "[mcp_servers.bugget]",
    `url = "${url}"`,
    'bearer_token_env_var = "BUGGET_PAT"',
  ].join("\n");

const toMcpAppContext = (
  workspaceId: string | number | null | undefined,
  teamId: string | number | null | undefined
): McpAppContext | null => {
  if (
    workspaceId === null ||
    workspaceId === undefined ||
    workspaceId === "" ||
    teamId === null ||
    teamId === undefined ||
    teamId === ""
  ) {
    return null;
  }

  return { workspaceId, teamId };
};

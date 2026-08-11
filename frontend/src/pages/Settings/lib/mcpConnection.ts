import { getAppContext } from "@/shared/api";

/** Плейсхолдер в сниппетах, пока значение токена ещё не показано. */
export const MCP_TOKEN_PLACEHOLDER = "bgt_pat_…";

/**
 * URL Streamable HTTP MCP для текущей пары workspace+team.
 * Сегменты берутся из контекста приложения — тот же scope, что у PAT.
 */
export const buildMcpEndpointUrl = (
  origin: string = typeof window !== "undefined" ? window.location.origin : ""
): string | null => {
  const { workspaceId, teamId } = getAppContext();
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

  return `${origin}/api/app/workspaces/${workspaceId}/teams/${teamId}/v1/mcp`;
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

/**
 * Готовый фрагмент для Claude Code (`.mcp.json` / `~/.claude.json`).
 * Поле `type: "http"` обязательно — без него клиент ждёт stdio.
 */
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

/**
 * Сниппет для Codex (`~/.codex/config.toml`). Токен — в env, не в файле:
 * `export BUGGET_PAT='…'`.
 */
export const buildCodexMcpSnippet = (url: string): string =>
  [
    "[mcp_servers.bugget]",
    `url = "${url}"`,
    'bearer_token_env_var = "BUGGET_PAT"',
  ].join("\n");

import { useCallback, useState } from "react";
import { copyToClipboard } from "@/shared/lib";
import {
  buildClaudeCodeMcpSnippet,
  buildCodexMcpSnippet,
  buildCursorMcpSnippet,
  buildMcpEndpointUrl,
  MCP_TOKEN_PLACEHOLDER,
  resolveMcpAppContext,
} from "../../../../lib/mcpConnection";

type Props = {
  /** Если передан — подставляется в JSON-сниппеты вместо плейсхолдера. */
  token?: string;
};

type ClientTab = "cursor" | "claude" | "codex";

const CLIENT_TABS: { id: ClientTab; label: string; fileHint: string }[] = [
  {
    id: "cursor",
    label: "Cursor",
    fileHint: "вставьте в ~/.cursor/mcp.json (или смержите mcpServers)",
  },
  {
    id: "claude",
    label: "Claude Code",
    fileHint: "вставьте в .mcp.json или ~/.claude.json",
  },
  { id: "codex", label: "Codex", fileHint: "~/.codex/config.toml" },
];

/**
 * Как подключить Bugget MCP: URL из текущего workspace/team и сниппеты
 * для Cursor, Claude Code и Codex.
 */
export const McpConnectionInstructions = ({ token }: Props) => {
  const [activeTab, setActiveTab] = useState<ClientTab>("cursor");
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const context = resolveMcpAppContext();
  const mcpUrl = buildMcpEndpointUrl();
  const tokenForSnippet = token ?? MCP_TOKEN_PLACEHOLDER;

  const snippetForTab = (tab: ClientTab): string | null => {
    if (mcpUrl === null) return null;
    if (tab === "cursor") return buildCursorMcpSnippet(mcpUrl, tokenForSnippet);
    if (tab === "claude")
      return buildClaudeCodeMcpSnippet(mcpUrl, tokenForSnippet);
    return buildCodexMcpSnippet(mcpUrl);
  };

  const copyValue = useCallback(async (key: string, value: string) => {
    try {
      await copyToClipboard(value);
      setCopiedKey(key);
    } catch (error) {
      console.error("Failed to copy MCP connection snippet", error);
    }
  }, []);

  const activeSnippet = snippetForTab(activeTab);
  const activeHint =
    CLIENT_TABS.find((tab) => tab.id === activeTab)?.fileHint ?? "";

  return (
    <details className="rounded-lg border border-base-300/60 bg-base-200/30">
      <summary className="cursor-pointer px-3 py-2 text-sm font-medium text-base-content">
        Подключение MCP (Cursor, Claude Code, Codex)
      </summary>
      <div className="space-y-3 border-t border-base-300/50 px-3 py-3 text-sm">
        <p className="text-base-content/70">
          URL:{" "}
          <code className="text-xs">
            {"{origin}/api/app/workspaces/{workspaceId}/teams/{teamId}/v1/mcp"}
          </code>
          . В{" "}
          <code className="text-xs">Authorization</code> передавайте{" "}
          <code className="text-xs">Bearer &lt;токен&gt;</code> — права как у
          этой же команды.
        </p>

        <ul className="list-disc space-y-1 pl-5 text-base-content/70">
          <li>
            <code className="text-xs">origin</code> — адрес Bugget в браузере
            (например{" "}
            <code className="text-xs">
              {typeof window !== "undefined"
                ? window.location.origin
                : "https://bugget.ati.st"}
            </code>
            ).
          </li>
          <li>
            <code className="text-xs">teamId</code> — id команды из адресной
            строки:{" "}
            <code className="text-xs">/teams/&lt;teamId&gt;/…</code>
            {context !== null && (
              <>
                {" "}
                (сейчас <code className="text-xs">{context.teamId}</code>)
              </>
            )}
            .
          </li>
          <li>
            <code className="text-xs">workspaceId</code> — id рабочего
            пространства команды. В self-hosted обычно{" "}
            <code className="text-xs">1</code>
            {context !== null && (
              <>
                ; сейчас <code className="text-xs">{context.workspaceId}</code>
              </>
            )}
            . На ATI смотрите его в Network у запросов{" "}
            <code className="text-xs">/api/app/workspaces/…</code>.
          </li>
        </ul>

        {mcpUrl === null ? (
          <p className="text-base-content/60">
            Откройте настройки из команды в сайдбаре (URL вида{" "}
            <code className="text-xs">/teams/&lt;teamId&gt;/settings</code>) —
            без teamId URL не собрать.
          </p>
        ) : (
          <>
            <div>
              <div className="mb-1 flex items-center justify-between gap-2">
                <span className="text-xs font-medium text-base-content/60">
                  URL для текущей команды (workspaceId=
                  {context?.workspaceId}, teamId={context?.teamId})
                </span>
                <button
                  type="button"
                  className="btn btn-ghost btn-xs"
                  onClick={() => copyValue("url", mcpUrl)}
                >
                  {copiedKey === "url" ? "Скопировано" : "Скопировать"}
                </button>
              </div>
              <code className="block break-all rounded-lg bg-base-200 p-2 font-mono text-xs">
                {mcpUrl}
              </code>
            </div>

            <div className="flex flex-wrap gap-1">
              {CLIENT_TABS.map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  className={`btn btn-xs ${
                    activeTab === tab.id ? "btn-primary" : "btn-ghost"
                  }`}
                  onClick={() => setActiveTab(tab.id)}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            <p className="text-xs text-base-content/60">{activeHint}</p>

            {activeTab === "codex" && (
              <p className="text-xs text-base-content/60">
                Токен в файл не кладите:{" "}
                <code className="text-xs">
                  export BUGGET_PAT=&apos;{tokenForSnippet}&apos;
                </code>
              </p>
            )}

            {activeSnippet !== null && (
              <div>
                <div className="mb-1 flex justify-end">
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => copyValue(activeTab, activeSnippet)}
                  >
                    {copiedKey === activeTab ? "Скопировано" : "Скопировать"}
                  </button>
                </div>
                <pre className="overflow-x-auto rounded-lg bg-base-200 p-2 font-mono text-xs whitespace-pre-wrap break-all">
                  {activeSnippet}
                </pre>
              </div>
            )}
          </>
        )}
      </div>
    </details>
  );
};

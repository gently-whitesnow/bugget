// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  getAppContext: vi.fn(),
  parseAppContextFromPath: vi.fn(),
}));

import { getAppContext, parseAppContextFromPath } from "@/shared/api";
import {
  buildClaudeCodeMcpSnippet,
  buildCodexMcpSnippet,
  buildCursorMcpSnippet,
  buildMcpEndpointUrl,
  MCP_TOKEN_PLACEHOLDER,
  resolveMcpAppContext,
} from "./mcpConnection";

const mockedGetAppContext = vi.mocked(getAppContext);
const mockedParseAppContextFromPath = vi.mocked(parseAppContextFromPath);

afterEach(() => {
  vi.clearAllMocks();
});

describe("resolveMcpAppContext", () => {
  it("берёт workspace/team из контекста приложения", () => {
    mockedGetAppContext.mockReturnValue({ workspaceId: 1, teamId: 2 });

    expect(resolveMcpAppContext("/teams/9/settings")).toEqual({
      workspaceId: 1,
      teamId: 2,
    });
    expect(mockedParseAppContextFromPath).not.toHaveBeenCalled();
  });

  it("если контекст пуст — вытаскивает teamId из /teams/:id, workspace=1", () => {
    mockedGetAppContext.mockReturnValue({
      workspaceId: null,
      teamId: null,
    });
    mockedParseAppContextFromPath.mockReturnValue({
      workspaceId: 1,
      teamId: 7,
    });

    expect(resolveMcpAppContext("/teams/7/settings")).toEqual({
      workspaceId: 1,
      teamId: 7,
    });
    expect(mockedParseAppContextFromPath).toHaveBeenCalledWith(
      "/teams/7/settings"
    );
  });
});

describe("buildMcpEndpointUrl", () => {
  it("собирает URL из origin и текущих workspace/team", () => {
    mockedGetAppContext.mockReturnValue({ workspaceId: 1, teamId: 2 });

    expect(buildMcpEndpointUrl("https://bugget.ati.st")).toBe(
      "https://bugget.ati.st/api/app/workspaces/1/teams/2/v1/mcp"
    );
  });

  it("без контекста команды URL не отдаёт — PAT к нему не привязать", () => {
    mockedGetAppContext.mockReturnValue({
      workspaceId: null,
      teamId: null,
    });
    mockedParseAppContextFromPath.mockReturnValue({
      workspaceId: null,
      teamId: null,
    });

    expect(buildMcpEndpointUrl("https://bugget.ati.st")).toBeNull();
  });
});

describe("сниппеты клиентов", () => {
  const url = "https://bugget.ati.st/api/app/workspaces/1/teams/2/v1/mcp";

  it("Cursor: готовый mcpServers с url и Bearer", () => {
    const snippet = buildCursorMcpSnippet(url, "bgt_pat_secret");
    expect(JSON.parse(snippet)).toEqual({
      mcpServers: {
        bugget: {
          url,
          headers: { Authorization: "Bearer bgt_pat_secret" },
        },
      },
    });
  });

  it("Claude Code: mcpServers + type http", () => {
    const snippet = buildClaudeCodeMcpSnippet(url);
    expect(JSON.parse(snippet)).toEqual({
      mcpServers: {
        bugget: {
          type: "http",
          url,
          headers: { Authorization: `Bearer ${MCP_TOKEN_PLACEHOLDER}` },
        },
      },
    });
  });

  it("Codex: url и имя env с токеном", () => {
    expect(buildCodexMcpSnippet(url)).toBe(
      [
        "[mcp_servers.bugget]",
        `url = "${url}"`,
        'bearer_token_env_var = "BUGGET_PAT"',
      ].join("\n")
    );
  });
});

// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";

const fetchPersonalAccessTokens = vi.fn();
const createPersonalAccessToken = vi.fn();
const revokePersonalAccessToken = vi.fn();
const notifyError = vi.fn();
const writeText = vi.fn();

vi.mock("@/entities/user", () => ({
  fetchPersonalAccessTokens: () => fetchPersonalAccessTokens(),
  createPersonalAccessToken: (request: unknown) =>
    createPersonalAccessToken(request),
  revokePersonalAccessToken: (tokenId: string) =>
    revokePersonalAccessToken(tokenId),
}));

vi.mock("@/shared/api", () => ({
  getAppContext: () => ({ workspaceId: 1, teamId: 2 }),
  parseAppContextFromPath: () => ({ workspaceId: 1, teamId: 2 }),
}));

vi.mock("@/shared/model", () => ({
  useNotifications: () => ({ notifyError }),
  notificationMessages: { errorRetry: "Попробуйте снова" },
}));

const { PersonalAccessTokensSection } = await import(
  "./PersonalAccessTokensSection"
);

const currentTeamToken = {
  id: "10",
  workspaceId: 1,
  teamId: 2,
  label: "mcp",
  tokenPrefix: "bgt_pat_abcd",
  createdAt: "2026-08-01T10:00:00Z",
  expiresAt: "2026-11-01T10:00:00Z",
  lastUsedAt: null,
};

const otherTeamToken = {
  ...currentTeamToken,
  id: "11",
  label: "ci",
  teamId: 9,
};

/** Ждём, пока догрузится список: до этого секция показывает спиннер. */
const renderSection = async () => {
  render(<PersonalAccessTokensSection />);
  await waitFor(() => expect(fetchPersonalAccessTokens).toHaveBeenCalled());
};

beforeEach(() => {
  fetchPersonalAccessTokens.mockResolvedValue([currentTeamToken]);
  createPersonalAccessToken.mockResolvedValue({
    token: "bgt_pat_secret-value",
    personalAccessToken: currentTeamToken,
  });
  revokePersonalAccessToken.mockResolvedValue(undefined);
  vi.stubGlobal(
    "confirm",
    vi.fn(() => true)
  );
  vi.stubGlobal("navigator", { clipboard: { writeText } });
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("список токенов", () => {
  it("показывает название и префикс", async () => {
    await renderSection();

    expect(await screen.findByText("mcp")).toBeDefined();
    expect(screen.getByText("bgt_pat_abcd")).toBeDefined();
  });

  it("помечает токен чужой команды: список приходит по всем командам", async () => {
    fetchPersonalAccessTokens.mockResolvedValue([
      currentTeamToken,
      otherTeamToken,
    ]);

    await renderSection();
    await screen.findByText("ci");

    expect(screen.getAllByText("Другая команда")).toHaveLength(1);
  });

  it("сообщает об ошибке загрузки и не выдаёт её за пустой список", async () => {
    fetchPersonalAccessTokens.mockRejectedValue(new Error("сеть"));
    vi.spyOn(console, "error").mockImplementation(() => {});

    await renderSection();

    expect(
      await screen.findByText("Не удалось загрузить токены")
    ).toBeDefined();
    expect(screen.queryByText("Токенов пока нет")).toBeNull();
  });

  it("после ошибки загрузку можно повторить", async () => {
    fetchPersonalAccessTokens.mockRejectedValueOnce(new Error("сеть"));
    vi.spyOn(console, "error").mockImplementation(() => {});

    await renderSection();
    fireEvent.click(await screen.findByText("Попробовать снова"));

    expect(await screen.findByText("mcp")).toBeDefined();
    expect(screen.queryByText("Не удалось загрузить токены")).toBeNull();
  });
});

describe("инструкция MCP", () => {
  it("в блоке выпуска показывает URL и откуда брать workspace/team", async () => {
    await renderSection();

    expect(
      await screen.findByText("Подключение MCP (Cursor, Claude Code, Codex)")
    ).toBeDefined();
    fireEvent.click(
      screen.getByText("Подключение MCP (Cursor, Claude Code, Codex)")
    );
    expect(
      screen.getByText(
        "http://localhost:3000/api/app/workspaces/1/teams/2/v1/mcp"
      )
    ).toBeDefined();
    expect(
      screen.getByText(/id команды из адресной строки/)
    ).toBeDefined();
    expect(
      screen.getByText(/id рабочего пространства команды/)
    ).toBeDefined();
  });
});

describe("выпуск токена", () => {
  const createToken = async (label: string) => {
    fireEvent.change(screen.getByPlaceholderText("Название, например mcp"), {
      target: { value: label },
    });
    fireEvent.click(screen.getByText("Выпустить токен"));
  };

  it("значение показывается один раз и пропадает после закрытия", async () => {
    await renderSection();

    await createToken("mcp");

    const secret = await screen.findByText("bgt_pat_secret-value");
    expect(secret).toBeDefined();
    expect(createPersonalAccessToken).toHaveBeenCalledWith({ label: "mcp" });

    fireEvent.click(screen.getByText("Готово"));

    await waitFor(() =>
      expect(screen.queryByText("bgt_pat_secret-value")).toBeNull()
    );
  });

  it("значение копируется в буфер", async () => {
    await renderSection();

    await createToken("mcp");
    await screen.findByText("bgt_pat_secret-value");
    const tokenCopyButton = screen.getByText("Готово").previousElementSibling;
    expect(tokenCopyButton).not.toBeNull();
    fireEvent.click(tokenCopyButton!);

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("bgt_pat_secret-value")
    );
  });

  it("список перезапрашивается после выпуска", async () => {
    await renderSection();

    await createToken("mcp");

    await waitFor(() =>
      expect(fetchPersonalAccessTokens).toHaveBeenCalledTimes(2)
    );
  });

  it("пустое название не уходит в сеть", async () => {
    await renderSection();

    await createToken("   ");

    expect(createPersonalAccessToken).not.toHaveBeenCalled();
  });
});

describe("отзыв токена", () => {
  it("после подтверждения зовёт ручку и перезапрашивает список", async () => {
    await renderSection();
    await screen.findByText("mcp");

    fireEvent.click(screen.getByText("Отозвать"));

    await waitFor(() =>
      expect(revokePersonalAccessToken).toHaveBeenCalledWith("10")
    );
    await waitFor(() =>
      expect(fetchPersonalAccessTokens).toHaveBeenCalledTimes(2)
    );
  });

  /**
   * Кнопки отзыва остальных строк не блокируются, поэтому обновлений списка
   * летит несколько сразу. Запоздавший ответ раннего запроса не должен вернуть
   * в список уже отозванный токен.
   */
  it("запоздавший ответ не возвращает отозванный токен", async () => {
    const pendingRefreshes: ((tokens: unknown[]) => void)[] = [];
    fetchPersonalAccessTokens
      .mockResolvedValueOnce([currentTeamToken, otherTeamToken])
      .mockImplementation(
        () => new Promise((resolve) => pendingRefreshes.push(resolve))
      );

    await renderSection();
    await screen.findByText("mcp");

    const revokeButtons = screen.getAllByText("Отозвать");
    fireEvent.click(revokeButtons[0]);
    fireEvent.click(revokeButtons[1]);
    await waitFor(() => expect(pendingRefreshes).toHaveLength(2));

    pendingRefreshes[1]([]);
    pendingRefreshes[0]([currentTeamToken, otherTeamToken]);

    await waitFor(() => expect(screen.queryByText("mcp")).toBeNull());
    expect(screen.queryByText("ci")).toBeNull();
  });

  /**
   * Повторный отзыв уже отозванного токена — 404, то есть ложная ошибка на
   * успешном действии. Поэтому строка остаётся заблокированной, пока её отзыв
   * не завершился, даже если отзыв соседней строки завершился раньше.
   */
  it("каждая строка блокируется на время своего отзыва", async () => {
    fetchPersonalAccessTokens.mockResolvedValue([
      currentTeamToken,
      otherTeamToken,
    ]);
    const finishRevoke: (() => void)[] = [];
    revokePersonalAccessToken.mockImplementation(
      () => new Promise<void>((resolve) => finishRevoke.push(resolve))
    );

    await renderSection();
    await screen.findByText("ci");

    const revokeButtons = screen.getAllByText("Отозвать");
    fireEvent.click(revokeButtons[0]);
    fireEvent.click(revokeButtons[1]);

    await waitFor(() =>
      expect(screen.queryAllByText("Отозвать")).toHaveLength(0)
    );

    finishRevoke.forEach((resolve) => resolve());
  });

  it("без подтверждения токен остаётся: отзыв необратим", async () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => false)
    );
    await renderSection();
    await screen.findByText("mcp");

    fireEvent.click(screen.getByText("Отозвать"));

    expect(revokePersonalAccessToken).not.toHaveBeenCalled();
  });
});

// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setAppContext } from "@/shared/api";

/**
 * Проверяется разбор ключа аватара в адрес картинки. Транспорт держит
 * `shared/api/users`, поэтому подменяются операции, а не адаптер axios.
 */

const getUser = vi.fn();
const listUsers = vi.fn();
const autocomplete = vi.fn();

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();

  return {
    ...actual,
    usersApi: {
      ...actual.usersApi,
      getUser: (...args: unknown[]) => getUser(...args),
      listUsers: (...args: unknown[]) => listUsers(...args),
      autocompleteUsers: (...args: unknown[]) => autocomplete(...args),
    },
  };
});

const { autocompleteUsers, fetchCurrentUser, getUsersByIds } = await import(
  "./users"
);

const contextPrefix = "/api/users/v1/workspaces/1/teams/2";

const user = (imageUrl: string | null) => ({
  id: "7",
  name: "Имя",
  imageUrl,
  workspaceRole: "admin",
  mattermostUserId: null,
});

beforeEach(() => {
  setAppContext(1, 2);
});

afterEach(() => {
  setAppContext(null, null);
  vi.clearAllMocks();
});

describe("текущий пользователь", () => {
  it("аватар берётся у ручки своего профиля", async () => {
    getUser.mockResolvedValue(user("avatars/7.png"));

    const result = await fetchCurrentUser(1, 2);

    expect(getUser).toHaveBeenCalledWith(1, 2);
    expect(result.imageUrl).toBe(
      `${contextPrefix}/users/avatar/content?v=avatars%2F7.png`
    );
    // Роль и привязка Mattermost доезжают целиком, не теряясь по дороге.
    expect(result.workspaceRole).toBe("admin");
    expect(result.mattermostUserId).toBeNull();
  });

  it("пустой ключ аватара остаётся отсутствием картинки", async () => {
    getUser.mockResolvedValue(user(null));

    expect((await fetchCurrentUser(1, 2)).imageUrl).toBeNull();
  });
});

describe("другие пользователи", () => {
  it("аватар берётся у ручки с идентификатором пользователя", async () => {
    listUsers.mockResolvedValue([user("avatars/7.png")]);

    const [result] = await getUsersByIds(1, 2, ["7"]);

    expect(result.imageUrl).toBe(
      `${contextPrefix}/users/7/avatar/content?v=avatars%2F7.png`
    );
  });

  it("подсказки: аватар разбирается у каждого найденного", async () => {
    autocomplete.mockResolvedValue({
      users: [{ id: "7", name: "Имя", imageUrl: "avatars/7.png" }],
      total: 1,
    });

    const result = await autocompleteUsers("им");

    expect(autocomplete).toHaveBeenCalledWith({
      searchString: "им",
      skip: 0,
      take: 10,
    });
    expect(result.users[0].imageUrl).toBe(
      `${contextPrefix}/users/7/avatar/content?v=avatars%2F7.png`
    );
  });
});

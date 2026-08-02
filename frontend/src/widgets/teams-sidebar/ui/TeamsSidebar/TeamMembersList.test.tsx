// @vitest-environment jsdom
import { createWatch, fork } from "effector";
import { Provider } from "effector-react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { $deletingUserId, $memberDetails, deleteMember } from "../../model";
import { TeamMembersList } from "./TeamMembersList";

const UNSAFE_USER_ID = "9007199254740993";

vi.mock("../../lib/useSidebarUser", () => ({
  useSidebarUser: () => ({
    user: { id: "1" },
    isAdmin: true,
  }),
}));

const member = {
  id: UNSAFE_USER_ID,
  name: "Большой идентификатор",
  imageUrl: null,
  workspaceRole: "member",
  mattermostUserId: null,
};

const renderList = (deletingUserId: string | null = null) => {
  const scope = fork({
    values: [
      [$memberDetails, [member]],
      [$deletingUserId, deletingUserId],
    ],
  });

  render(
    <Provider value={scope}>
      <TeamMembersList />
    </Provider>
  );

  return scope;
};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("удаление участника команды", () => {
  it("передаёт строковый id за пределом safe integer без округления", () => {
    vi.stubGlobal(
      "confirm",
      vi.fn(() => true)
    );
    const scope = renderList();
    const deleted = vi.fn();
    createWatch({ unit: deleteMember, fn: deleted, scope });

    fireEvent.click(screen.getByTitle("Удалить участника"));

    expect(deleted).toHaveBeenCalledWith({
      userId: UNSAFE_USER_ID,
      userName: member.name,
    });
  });

  it("сопоставляет loading-key с тем же строковым id", () => {
    renderList(UNSAFE_USER_ID);

    const button = screen.getByTitle("Удалить участника") as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.querySelector(".loading-spinner")).not.toBeNull();
  });
});

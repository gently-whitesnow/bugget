// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

import { BugStatuses } from "@/shared/config";

const requestFix = vi.fn();

vi.mock("effector-react", () => ({
  useUnit: (unit: unknown) =>
    unit === "report-id-store" ? "team-42" : requestFix,
}));

vi.mock("@/entities/report", () => ({
  $reportIdStore: "report-id-store",
}));

vi.mock("@/pages/Report/model-bug", () => ({
  requestBugFixFx: "request-bug-fix-fx",
}));

const { default: BugFixRequestButton } = await import("./BugFixRequestButton");

const button = () =>
  screen.getByRole("button", { name: /Исправить баг/ }) as HTMLButtonElement;

describe("BugFixRequestButton", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("клик зовёт запрос и дизейблит кнопку до смены статуса", () => {
    requestFix.mockReturnValue(new Promise(() => {}));
    const view = render(
      <BugFixRequestButton bugId={7} status={BugStatuses.OPEN} />
    );

    fireEvent.click(button());

    expect(requestFix).toHaveBeenCalledWith({ reportId: "team-42", bugId: 7 });
    expect(button().disabled).toBe(true);

    // Повторный клик по задизейбленной кнопке не плодит запросов.
    fireEvent.click(button());
    expect(requestFix).toHaveBeenCalledTimes(1);

    // Статус бага изменился (руками или по сокету) — кнопка оживает сама.
    view.rerender(<BugFixRequestButton bugId={7} status={BugStatuses.FIXED} />);
    expect(button().disabled).toBe(false);
  });

  it("отказ backend'а оживляет кнопку сразу, не дожидаясь смены статуса", async () => {
    requestFix.mockRejectedValue(new Error("502"));
    render(<BugFixRequestButton bugId={7} status={BugStatuses.OPEN} />);

    fireEvent.click(button());

    // Ошибка обрабатывается в catch внутри обработчика клика.
    await vi.waitFor(() => expect(button().disabled).toBe(false));
  });
});

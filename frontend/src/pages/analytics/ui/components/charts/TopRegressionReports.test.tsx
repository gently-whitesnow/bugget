// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useSearchParams } from "react-router";

import TopRegressionReports from "./TopRegressionReports";

/**
 * Навигация по аналитике держится на `report_id`, который после MAIN-44 приходит
 * строкой канона `Int64String`. Проверяется вся цепочка от ответа до адреса:
 * значение показано, положено в `key` и уехало в `?report=` цифра в цифру.
 *
 * `9007199254740993` выбран не случайно: через `Number(...)` он превращается в
 * `...992`, то есть открылся бы соседний репорт, и заметить это в UI нечем.
 */
const UNSAFE = "9007199254740993";

const Query = () => (
  <span data-testid="query">{useSearchParams()[0].get("report")}</span>
);

const renderChart = () =>
  render(
    <MemoryRouter initialEntries={["/analytics?section=overview"]}>
      <Routes>
        <Route
          path="/analytics"
          element={
            <>
              <TopRegressionReports
                reports={[
                  {
                    reportId: UNSAFE,
                    title: "Падает карточка",
                    regressionCycles: 2,
                  },
                ]}
              />
              <Query />
            </>
          }
        />
      </Routes>
    </MemoryRouter>
  );

describe("топ регрессионных репортов", () => {
  it("идентификатор показан и уходит в адрес без округления", () => {
    renderChart();

    expect(screen.getByText(`#${UNSAFE}`)).toBeDefined();

    fireEvent.click(screen.getByRole("button"));

    expect(screen.getByTestId("query").textContent).toBe(UNSAFE);
    expect(screen.getByTestId("query").textContent).not.toBe(
      "9007199254740992"
    );
  });
});

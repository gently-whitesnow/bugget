// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useHotkey } from "./useHotkey";

const Probe = ({
  onHotkey,
  inEditable,
  enabled,
}: {
  onHotkey: () => void;
  inEditable?: boolean;
  enabled?: boolean;
}) => {
  useHotkey({ code: "Slash" }, onHotkey, { inEditable, enabled });
  return <input aria-label="field" />;
};

describe("useHotkey", () => {
  afterEach(() => {
    cleanup();
  });

  it("fires outside of editable fields", () => {
    const onHotkey = vi.fn();
    render(<Probe onHotkey={onHotkey} />);

    fireEvent.keyDown(document.body, { key: "/", code: "Slash" });

    expect(onHotkey).toHaveBeenCalledTimes(1);
  });

  it("ignores typing in fields unless allowed", () => {
    const onHotkey = vi.fn();
    const { rerender } = render(<Probe onHotkey={onHotkey} />);
    const field = screen.getByLabelText("field");

    fireEvent.keyDown(field, { key: "/", code: "Slash" });
    expect(onHotkey).not.toHaveBeenCalled();

    rerender(<Probe onHotkey={onHotkey} inEditable />);
    fireEvent.keyDown(field, { key: "/", code: "Slash" });
    expect(onHotkey).toHaveBeenCalledTimes(1);
  });

  it("skips events already handled by a field", () => {
    const onHotkey = vi.fn();
    render(<Probe onHotkey={onHotkey} inEditable />);
    const field = screen.getByLabelText("field");
    field.addEventListener("keydown", (event) => event.preventDefault());

    fireEvent.keyDown(field, { key: "/", code: "Slash" });

    expect(onHotkey).not.toHaveBeenCalled();
  });

  it("does nothing when disabled", () => {
    const onHotkey = vi.fn();
    render(<Probe onHotkey={onHotkey} enabled={false} />);

    fireEvent.keyDown(document.body, { key: "/", code: "Slash" });

    expect(onHotkey).not.toHaveBeenCalled();
  });
});

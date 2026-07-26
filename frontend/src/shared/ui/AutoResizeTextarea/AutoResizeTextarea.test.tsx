// @vitest-environment jsdom
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import AutoResizeTextarea from "./AutoResizeTextarea";

const getTextarea = () => screen.getByRole("textbox") as HTMLTextAreaElement;

describe("AutoResizeTextarea", () => {
  let onChange: ReturnType<typeof vi.fn<(value: string) => void>>;

  beforeEach(() => {
    onChange = vi.fn<(value: string) => void>();
  });

  afterEach(() => {
    cleanup();
  });

  describe("rendering", () => {
    it("renders with initial value", () => {
      render(<AutoResizeTextarea value="hello" onChange={onChange} />);
      expect(getTextarea().value).toBe("hello");
    });

    it("renders with placeholder", () => {
      render(
        <AutoResizeTextarea
          value=""
          onChange={onChange}
          placeholder="Type here"
        />
      );
      expect(getTextarea().placeholder).toBe("Type here");
    });

    it("applies maxLength attribute", () => {
      render(
        <AutoResizeTextarea value="" onChange={onChange} maxLength={100} />
      );
      expect(getTextarea().maxLength).toBe(100);
    });
  });

  describe("user input", () => {
    it("calls onChange on input", () => {
      render(<AutoResizeTextarea value="a" onChange={onChange} />);
      fireEvent.change(getTextarea(), { target: { value: "ab" } });
      expect(onChange).toHaveBeenCalledWith("ab");
    });

    it("preserves DOM value after controlled re-render cycle", () => {
      const { rerender } = render(
        <AutoResizeTextarea value="start" onChange={onChange} />
      );

      fireEvent.change(getTextarea(), { target: { value: "typed" } });

      rerender(<AutoResizeTextarea value="typed" onChange={onChange} />);

      expect(getTextarea().value).toBe("typed");
    });
  });

  describe("external value sync", () => {
    it("updates DOM when value prop changes externally", () => {
      const { rerender } = render(
        <AutoResizeTextarea value="old" onChange={onChange} />
      );

      rerender(
        <AutoResizeTextarea value="new from server" onChange={onChange} />
      );

      expect(getTextarea().value).toBe("new from server");
    });
  });

  describe("Enter (save)", () => {
    it("calls onSave with current DOM value", () => {
      const onSave = vi.fn();
      render(
        <AutoResizeTextarea value="init" onChange={onChange} onSave={onSave} />
      );

      fireEvent.change(getTextarea(), { target: { value: "save me" } });
      fireEvent.keyDown(getTextarea(), { key: "Enter" });

      expect(onSave).toHaveBeenCalledWith("save me");
    });

    it("does not trigger onSave on Shift+Enter", () => {
      const onSave = vi.fn();
      render(
        <AutoResizeTextarea value="" onChange={onChange} onSave={onSave} />
      );

      fireEvent.keyDown(getTextarea(), { key: "Enter", shiftKey: true });

      expect(onSave).not.toHaveBeenCalled();
    });

    it("suppresses onBlur after save", () => {
      const onBlur = vi.fn();
      const onSave = vi.fn();
      render(
        <AutoResizeTextarea
          value=""
          onChange={onChange}
          onBlur={onBlur}
          onSave={onSave}
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Enter" });
      fireEvent.blur(getTextarea());

      expect(onSave).toHaveBeenCalled();
      expect(onBlur).not.toHaveBeenCalled();
    });
  });

  describe("Escape (cancel)", () => {
    it("rolls back to rollbackValue", () => {
      const onCancel = vi.fn();
      render(
        <AutoResizeTextarea
          value="edited"
          onChange={onChange}
          onCancel={onCancel}
          rollbackValue="original"
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Escape" });

      expect(getTextarea().value).toBe("original");
      expect(onCancel).toHaveBeenCalledWith("original");
    });

    it("rolls back to focus-time value when no rollbackValue", () => {
      const onCancel = vi.fn();
      const { rerender } = render(
        <AutoResizeTextarea
          value="at-focus"
          onChange={onChange}
          onCancel={onCancel}
        />
      );

      fireEvent.focus(getTextarea());

      fireEvent.change(getTextarea(), { target: { value: "changed" } });
      rerender(
        <AutoResizeTextarea
          value="changed"
          onChange={onChange}
          onCancel={onCancel}
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Escape" });

      expect(getTextarea().value).toBe("at-focus");
      expect(onCancel).toHaveBeenCalledWith("at-focus");
    });

    it("calls onChange when rollback differs from current value", () => {
      render(
        <AutoResizeTextarea
          value="edited"
          onChange={onChange}
          rollbackValue="original"
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Escape" });

      expect(onChange).toHaveBeenCalledWith("original");
    });

    it("suppresses onBlur after cancel", () => {
      const onBlur = vi.fn();
      const onCancel = vi.fn();
      render(
        <AutoResizeTextarea
          value="v"
          onChange={onChange}
          onBlur={onBlur}
          onCancel={onCancel}
          rollbackValue="v"
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Escape" });
      fireEvent.blur(getTextarea());

      expect(onCancel).toHaveBeenCalled();
      expect(onBlur).not.toHaveBeenCalled();
    });
  });

  describe("blur", () => {
    it("calls onBlur with current DOM value", () => {
      const onBlur = vi.fn();
      render(
        <AutoResizeTextarea value="init" onChange={onChange} onBlur={onBlur} />
      );

      fireEvent.change(getTextarea(), { target: { value: "blurred" } });
      fireEvent.blur(getTextarea());

      expect(onBlur).toHaveBeenCalledWith("blurred");
    });
  });

  describe("focus", () => {
    it("calls onFocus with current DOM value", () => {
      const onFocus = vi.fn();
      render(
        <AutoResizeTextarea
          value="focused"
          onChange={onChange}
          onFocus={onFocus}
        />
      );

      fireEvent.focus(getTextarea());

      expect(onFocus).toHaveBeenCalledWith("focused");
    });
  });

  describe("external onKeyDown", () => {
    it("delegates to external onKeyDown handler", () => {
      const onKeyDown = vi.fn();
      render(
        <AutoResizeTextarea
          value=""
          onChange={onChange}
          onKeyDown={onKeyDown}
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "a" });

      expect(onKeyDown).toHaveBeenCalled();
    });

    it("skips internal handling when external handler prevents default", () => {
      const onSave = vi.fn();
      const onKeyDown = vi.fn((e: React.KeyboardEvent) => e.preventDefault());
      render(
        <AutoResizeTextarea
          value=""
          onChange={onChange}
          onKeyDown={onKeyDown}
          onSave={onSave}
        />
      );

      fireEvent.keyDown(getTextarea(), { key: "Enter" });

      expect(onKeyDown).toHaveBeenCalled();
      expect(onSave).not.toHaveBeenCalled();
    });
  });
});

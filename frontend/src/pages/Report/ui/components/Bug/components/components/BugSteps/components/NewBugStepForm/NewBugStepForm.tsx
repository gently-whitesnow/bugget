import { useState, useCallback } from "react";
import { useUnit } from "effector-react";

import { ComposerInput } from "@/shared/ui";
import { composerText } from "@/pages/Report/lib";
import { createBugStepFx } from "@/pages/Report/model-bug-step";
import { bugStepMaxLength } from "@/shared/config";

type Props = {
  stepNumber: number;
  reportId: string | null;
  bugId: number;
  disabled?: boolean;
};

const NewBugStepForm = ({
  stepNumber,
  reportId,
  bugId,
  disabled = false,
}: Props) => {
  const [text, setText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const createStep = useUnit(createBugStepFx);

  const handleCreateStep = useCallback(
    async (files: File[]) => {
      if (!reportId) return false;
      if (!text.trim() && files.length === 0) return false;

      setIsSubmitting(true);

      try {
        const payload = { text: composerText(text) };
        await createStep({ reportId, bugId, payload, files });
        setText("");
        return true;
      } catch {
        return false;
      } finally {
        setIsSubmitting(false);
      }
    },
    [text, reportId, bugId, createStep]
  );

  return (
    <div className="group flex items-start gap-3">
      <div className="w-8 h-8 rounded-full border-2 border-base-300 bg-base-200 flex items-center justify-center flex-shrink-0 transition-colors group-focus-within:border-secondary">
        <span className="text-sm font-medium text-base-content/70">
          {stepNumber}
        </span>
      </div>
      <div className="flex-1">
        <ComposerInput
          value={text}
          onChange={setText}
          onSend={handleCreateStep}
          disabled={disabled || !reportId}
          placeholder="Опишите шаг..."
          isSubmitting={isSubmitting}
          enableAttachments
          maxLength={bugStepMaxLength}
        />
      </div>
    </div>
  );
};

export default NewBugStepForm;

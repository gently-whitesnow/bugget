import { DragEvent, useCallback, useEffect, useState, useRef } from "react";
import { flushSync } from "react-dom";
import { Footprints } from "lucide-react";
import { useStoreMap, useUnit } from "effector-react";

import {
  $bugStepsStore,
  deleteBugStepEvent,
  patchBugStepEvent,
  updateBugStepsOrderEvent,
} from "@/pages/Report/model-bug-step";
import {
  bugStepHashPattern,
  getBugStepElementId,
  useScrollToNestedHashHighlight,
} from "@/pages/Report/lib";
import { useLayout } from "@/shared/lib";
import { BugStep } from "@/entities/report";
import { SectionHeaderChip } from "@/shared/ui";
import BugStepItem from "./components/BugStepItem/BugStepItem";
import NewBugStepForm from "./components/NewBugStepForm/NewBugStepForm";

const moveStep = (
  steps: BugStep[],
  sourceId: number,
  targetId: number
): BugStep[] => {
  const updated = [...steps];
  const fromIndex = updated.findIndex((step) => step.id === sourceId);
  const toIndex = updated.findIndex((step) => step.id === targetId);

  if (fromIndex === -1 || toIndex === -1) return steps;

  const [moved] = updated.splice(fromIndex, 1);
  updated.splice(toIndex, 0, moved);

  return updated;
};

const isSameOrder = (a: number[], b: number[]) =>
  a.length === b.length && a.every((id, idx) => id === b[idx]);

type Props = {
  reportId: string | null;
  bugId: number;
  disabled?: boolean;
  resolved?: boolean;
};

const BugSteps = ({
  reportId,
  bugId,
  disabled = false,
  resolved = false,
}: Props) => {
  const { scrollContainerRef } = useLayout();
  const [patchBugStep, deleteBugStep, updateBugStepsOrder] = useUnit([
    patchBugStepEvent,
    deleteBugStepEvent,
    updateBugStepsOrderEvent,
  ]);

  const steps = useStoreMap({
    store: $bugStepsStore,
    keys: [bugId],
    fn: (state, [id]) => state[id] || [],
  });

  const [orderedSteps, setOrderedSteps] = useState<BugStep[]>(steps);
  const [draggedId, setDraggedId] = useState<number | null>(null);
  const [isExpanded, setIsExpanded] = useState(false);
  const [highlightedStepId, setHighlightedStepId] = useState<number | null>(
    null
  );

  const initialized = useRef(false);

  useEffect(() => {
    initialized.current = false;
  }, [bugId]);

  useEffect(() => {
    if (!initialized.current && steps.length > 0 && !resolved) {
      setIsExpanded(true);
      initialized.current = true;
    }
  }, [steps.length, resolved]);

  useEffect(() => {
    setOrderedSteps(steps);
  }, [steps]);
  const getStepId = useCallback((step: BugStep) => step.id, []);

  useScrollToNestedHashHighlight({
    parentId: bugId,
    items: steps,
    getItemId: getStepId,
    hashPattern: bugStepHashPattern,
    getElementId: getBugStepElementId,
    setIsExpanded,
    setHighlightedId: setHighlightedStepId,
    scrollContainerRef,
  });

  const handleUpdateStep = (stepId: number, text: string) => {
    if (disabled || !reportId) return;
    patchBugStep({ reportId, bugId, stepId, text });
  };

  const handleDeleteStep = (stepId: number) => {
    if (disabled || !reportId) return;
    deleteBugStep({ reportId, bugId, stepId });
  };

  const handleDragStart = (stepId: number, event: DragEvent) => {
    if (disabled) return;
    setDraggedId(stepId);
    event.dataTransfer.effectAllowed = "move";
  };

  const handleDragOver = (stepId: number, event: DragEvent) => {
    if (draggedId === null || draggedId === stepId) return;

    event.preventDefault();

    const currentIds = orderedSteps.map((s) => s.id);
    const newSteps = moveStep(orderedSteps, draggedId, stepId);
    const newIds = newSteps.map((s) => s.id);

    if (isSameOrder(currentIds, newIds)) return;

    const update = () => setOrderedSteps(newSteps);

    if (
      "startViewTransition" in document &&
      typeof document.startViewTransition === "function"
    ) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (document as any).startViewTransition(() => {
        flushSync(update);
      });
    } else {
      update();
    }
  };

  const handleDragEnd = () => {
    if (draggedId === null) return;

    const newOrder = orderedSteps.map((step) => step.id);
    const currentOrder = steps.map((step) => step.id);

    setDraggedId(null);

    if (!reportId || disabled || isSameOrder(newOrder, currentOrder)) {
      setOrderedSteps(steps);
      return;
    }

    updateBugStepsOrder({ reportId, bugId, stepIds: newOrder });
  };

  const isReorderable = !disabled && orderedSteps.length > 1;

  if (!isExpanded) {
    return (
      <SectionHeaderChip
        count={orderedSteps.length}
        icon={<Footprints className="w-3 h-3 text-info" />}
        texts={{
          zero: "Добавить шаги воспроизведения",
          one: "шаг воспроизведения",
          few: "шага воспроизведения",
          many: "шагов воспроизведения",
        }}
        onClick={() => setIsExpanded(true)}
        disabled={disabled}
      />
    );
  }

  return (
    <div className="w-full bg-base-100 border border-base-300 rounded-lg pt-3 px-3 flex flex-col">
      <SectionHeaderChip
        count={orderedSteps.length}
        icon={<Footprints className="w-3 h-3 text-info" />}
        texts={{
          zero: "Шаги воспроизведения",
          one: "шаг воспроизведения",
          few: "шага воспроизведения",
          many: "шагов воспроизведения",
        }}
        onClick={() => setIsExpanded(false)}
        disabled={disabled}
      />

      <div>
        {orderedSteps.map((step, index) => (
          <BugStepItem
            key={step.id}
            step={step}
            index={index}
            reportId={reportId!}
            bugId={bugId}
            disabled={disabled}
            isReorderable={isReorderable}
            isDragging={draggedId === step.id}
            isHighlighted={highlightedStepId === step.id}
            onUpdate={handleUpdateStep}
            onDelete={handleDeleteStep}
            onDragStart={handleDragStart}
            onDragOver={handleDragOver}
            onDrop={handleDragEnd}
          />
        ))}
      </div>

      <div className="p-2">
        <NewBugStepForm
          stepNumber={orderedSteps.length + 1}
          reportId={reportId}
          bugId={bugId}
          disabled={disabled}
        />
      </div>
    </div>
  );
};

export default BugSteps;

import { createEffect, createEvent, createStore, sample } from "effector";

import {
  createBugStep,
  deleteBugStep,
  patchBugStep,
  updateBugStepsOrder,
  createBugStepAttachment,
  deleteBugStepAttachment,
  renameBugStepAttachment,
} from "@/entities/report";
import type { BugStepRequest, BugStepResponse } from "@/entities/report";
import { attachmentFromSocket, bugStepFromSocket } from "@/entities/report";
import { AttachmentTypes } from "@/shared/config";
import type { BugStep, Attachment } from "@/entities/report";
import type {
  AttachmentSocketResponse,
  BugStepSocketResponse,
} from "@/shared/model";

const sortSteps = (steps: BugStep[]) =>
  [...steps].sort((a, b) => a.stepNumber - b.stepNumber);

/**
 * Обновляет шаг по его id, не зная бага: события SignalR о вложениях приносят
 * только `entityId` шага. Если шага нет или обновление ничего не меняет
 * (`patch` вернул `null`), стор остаётся прежним.
 */
const patchStepById = (
  state: Record<number, BugStep[]>,
  stepId: number,
  patch: (step: BugStep) => BugStep | null
): Record<number, BugStep[]> => {
  for (const [bugId, steps] of Object.entries(state)) {
    const idx = steps.findIndex((step) => step.id === stepId);
    if (idx === -1) continue;

    const updated = patch(steps[idx]);
    if (!updated) return state;

    return {
      ...state,
      [Number(bugId)]: [
        ...steps.slice(0, idx),
        updated,
        ...steps.slice(idx + 1),
      ],
    };
  }

  return state;
};

/**
 * Сторы
 */
export const $bugStepsStore = createStore<Record<number, BugStep[]>>({});

/**
 * Эффекты
 */
export const createBugStepFx = createEffect<
  { reportId: string; bugId: number; payload: BugStepRequest },
  BugStep
>(async ({ reportId, bugId, payload }) => {
  return await createBugStep(reportId, bugId, payload);
});

export const patchBugStepFx = createEffect<
  { reportId: string; bugId: number; stepId: number; payload: BugStepRequest },
  BugStep
>(async ({ reportId, bugId, stepId, payload }) => {
  return await patchBugStep(reportId, bugId, stepId, payload);
});

export const deleteBugStepFx = createEffect<
  { reportId: string; bugId: number; stepId: number },
  { bugId: number; stepId: number }
>(async ({ reportId, bugId, stepId }) => {
  await deleteBugStep(reportId, bugId, stepId);
  return { bugId, stepId };
});

export const updateBugStepsOrderFx = createEffect<
  { reportId: string; bugId: number; stepIds: number[] },
  { bugId: number; steps: BugStep[] }
>(async ({ reportId, bugId, stepIds }) => {
  const steps = await updateBugStepsOrder(reportId, bugId, { stepIds });
  return { bugId, steps };
});

export const createBugStepAttachmentFx = createEffect<
  { reportId: string; bugId: number; stepId: number; file: File },
  { bugId: number; stepId: number; attachment: Attachment }
>(async ({ reportId, bugId, stepId, file }) => {
  const attachment = await createBugStepAttachment(
    reportId,
    bugId,
    stepId,
    file
  );
  return { bugId, stepId, attachment };
});

export const deleteBugStepAttachmentFx = createEffect<
  { reportId: string; bugId: number; stepId: number; attachmentId: number },
  { bugId: number; stepId: number; attachmentId: number }
>(async ({ reportId, bugId, stepId, attachmentId }) => {
  await deleteBugStepAttachment(reportId, bugId, stepId, attachmentId);
  return { bugId, stepId, attachmentId };
});

export const renameBugStepAttachmentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    stepId: number;
    attachmentId: number;
    fileName: string;
  },
  { bugId: number; stepId: number; attachment: Attachment }
>(async ({ reportId, bugId, stepId, attachmentId, fileName }) => {
  const attachment = await renameBugStepAttachment({
    reportId,
    bugId,
    stepId,
    attachmentId,
    fileName,
  });
  return { bugId, stepId, attachment };
});

/**
 * События
 */
export const setBugStepsEvent =
  createEvent<{ bugId: number; steps: BugStepResponse[] }[]>();

export const createBugStepEvent = createEvent<{
  reportId: string;
  bugId: number;
  text: string;
}>();

export const patchBugStepEvent = createEvent<{
  reportId: string;
  bugId: number;
  stepId: number;
  text: string;
}>();

export const deleteBugStepEvent = createEvent<{
  reportId: string;
  bugId: number;
  stepId: number;
}>();

export const updateBugStepsOrderEvent = createEvent<{
  reportId: string;
  bugId: number;
  stepIds: number[];
}>();

// socket события: payload описан типами realtime-контракта (`events.yaml`), а не
// HTTP-схемами; в сущности стора его переводят адаптеры `*FromSocket` (ADR-0007).
export const createBugStepSocketEvent = createEvent<BugStepSocketResponse>();
export const patchBugStepSocketEvent = createEvent<{
  bugId: number;
  step: BugStepSocketResponse;
}>();
export const deleteBugStepSocketEvent = createEvent<{
  bugId: number;
  stepId: number;
}>();
export const updateBugStepsOrderSocketEvent = createEvent<{
  bugId: number;
  steps: BugStepSocketResponse[];
}>();
export const createBugStepAttachmentSocketEvent =
  createEvent<AttachmentSocketResponse>();
export const bugStepAttachmentChangedSocketEvent =
  createEvent<AttachmentSocketResponse>();
export const deleteBugStepAttachmentSocketEvent = createEvent<{
  stepId: number;
  attachmentId: number;
}>();

/**
 * Логика
 */
$bugStepsStore
  .on(setBugStepsEvent, (state, payload) => {
    const updatedState = { ...state };
    payload.forEach(({ bugId, steps }) => {
      updatedState[bugId] = sortSteps(steps);
    });
    return updatedState;
  })
  .on(createBugStepFx.doneData, (state, step) => {
    const existingSteps = state[step.bugId] || [];
    if (existingSteps.some((item) => item.id === step.id)) return state;
    return { ...state, [step.bugId]: sortSteps([...existingSteps, step]) };
  })
  .on(patchBugStepFx.doneData, (state, step) => {
    const existingSteps = state[step.bugId] || [];
    return {
      ...state,
      [step.bugId]: existingSteps.map((item) =>
        item.id === step.id
          ? {
              ...item,
              ...step,
              attachments: step.attachments || item.attachments,
            }
          : item
      ),
    };
  })
  .on(deleteBugStepFx.doneData, (state, { bugId, stepId }) => {
    const existingSteps = state[bugId] || [];
    return {
      ...state,
      [bugId]: existingSteps.filter((item) => item.id !== stepId),
    };
  })
  .on(updateBugStepsOrderFx.doneData, (state, { bugId, steps }) => {
    const currentSteps = state[bugId] || [];
    const attachmentsMap = new Map(
      currentSteps.map((step) => [step.id, step.attachments])
    );

    const mergedSteps = steps.map((step) => ({
      ...step,
      attachments: step.attachments || attachmentsMap.get(step.id) || null,
    }));

    return {
      ...state,
      [bugId]: sortSteps(mergedSteps),
    };
  })
  .on(
    createBugStepAttachmentFx.doneData,
    (state, { bugId, stepId, attachment }) => {
      const existingSteps = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingSteps.map((step) => {
          if (step.id !== stepId) return step;
          const currentAttachments = step.attachments || [];
          if (currentAttachments.some((a) => a.id === attachment.id))
            return step;
          return {
            ...step,
            attachments: [...currentAttachments, attachment],
          };
        }),
      };
    }
  )
  .on(
    deleteBugStepAttachmentFx.doneData,
    (state, { bugId, stepId, attachmentId }) => {
      const existingSteps = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingSteps.map((step) =>
          step.id === stepId
            ? {
                ...step,
                attachments: (step.attachments || []).filter(
                  (a) => a.id !== attachmentId
                ),
              }
            : step
        ),
      };
    }
  )
  .on(
    renameBugStepAttachmentFx.doneData,
    (state, { bugId, stepId, attachment }) => {
      const existingSteps = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingSteps.map((step) =>
          step.id === stepId
            ? {
                ...step,
                attachments: (step.attachments || []).map((item) =>
                  item.id === attachment.id ? { ...item, ...attachment } : item
                ),
              }
            : step
        ),
      };
    }
  )
  .on(createBugStepSocketEvent, (state, payload) => {
    const mappedStep = bugStepFromSocket(payload);
    const existingSteps = state[mappedStep.bugId] || [];
    if (existingSteps.some((item) => item.id === mappedStep.id)) return state;

    return {
      ...state,
      [mappedStep.bugId]: sortSteps([...existingSteps, mappedStep]),
    };
  })
  .on(patchBugStepSocketEvent, (state, { bugId, step }) => {
    const existingSteps = state[bugId] || [];
    if (!existingSteps.length) return state;

    const mappedStep = bugStepFromSocket({ ...step, bugId });

    return {
      ...state,
      [bugId]: existingSteps.map((item) =>
        item.id === mappedStep.id ? { ...item, ...mappedStep } : item
      ),
    };
  })
  .on(deleteBugStepSocketEvent, (state, { bugId, stepId }) => {
    const existingSteps = state[bugId] || [];
    if (!existingSteps.length) return state;

    return {
      ...state,
      [bugId]: existingSteps.filter((item) => item.id !== stepId),
    };
  })
  .on(updateBugStepsOrderSocketEvent, (state, { bugId, steps }) => {
    const currentSteps = state[bugId] || [];
    const attachmentsMap = new Map(
      currentSteps.map((step) => [step.id, step.attachments])
    );

    const mergedSteps = steps.map((step) => {
      const mapped = bugStepFromSocket(step);
      return {
        ...mapped,
        attachments:
          mapped.attachments || attachmentsMap.get(mapped.id) || null,
      };
    });

    return {
      ...state,
      [bugId]: sortSteps(mergedSteps),
    };
  })
  .on(createBugStepAttachmentSocketEvent, (state, payload) => {
    if (payload.attachType !== AttachmentTypes.BUG_STEP) return state;

    const attachment = attachmentFromSocket(payload);

    return patchStepById(state, payload.entityId, (step) => {
      const currentAttachments = step.attachments || [];
      if (currentAttachments.some((a) => a.id === attachment.id)) return null;

      return { ...step, attachments: [...currentAttachments, attachment] };
    });
  })
  .on(bugStepAttachmentChangedSocketEvent, (state, payload) => {
    if (payload.attachType !== AttachmentTypes.BUG_STEP) return state;

    const attachment = attachmentFromSocket(payload);

    return patchStepById(state, payload.entityId, (step) => ({
      ...step,
      attachments: (step.attachments || []).map((item) =>
        item.id === attachment.id ? attachment : item
      ),
    }));
  })
  .on(deleteBugStepAttachmentSocketEvent, (state, { stepId, attachmentId }) =>
    patchStepById(state, stepId, (step) => ({
      ...step,
      attachments: (step.attachments || []).filter(
        (item) => item.id !== attachmentId
      ),
    }))
  );

/**
 * Сэмплы
 */
sample({
  clock: createBugStepEvent,
  fn: ({ reportId, bugId, text }) => ({
    reportId,
    bugId,
    payload: { text },
  }),
  target: createBugStepFx,
});

sample({
  clock: patchBugStepEvent,
  fn: ({ reportId, bugId, stepId, text }) => ({
    reportId,
    bugId,
    stepId,
    payload: { text },
  }),
  target: patchBugStepFx,
});

sample({
  clock: deleteBugStepEvent,
  target: deleteBugStepFx,
});

sample({
  clock: updateBugStepsOrderEvent,
  target: updateBugStepsOrderFx,
});

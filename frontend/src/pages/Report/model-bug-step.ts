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
import { AttachmentTypes } from "@/shared/config";
import type { BugStep, Attachment } from "@/entities/report";

const sortSteps = (steps: BugStep[]) =>
  [...steps].sort((a, b) => a.stepNumber - b.stepNumber);

const mapStepResponse = (step: BugStepResponse): BugStep => ({
  id: step.id,
  bugId: step.bugId,
  text: step.text,
  stepNumber: step.stepNumber,
  creatorUserId: step.creatorUserId,
  createdAt: step.createdAt,
  updatedAt: step.updatedAt,
  attachments: step.attachments || null,
});

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
  const step = await createBugStep(reportId, bugId, payload);
  return mapStepResponse(step);
});

export const patchBugStepFx = createEffect<
  { reportId: string; bugId: number; stepId: number; payload: BugStepRequest },
  BugStep
>(async ({ reportId, bugId, stepId, payload }) => {
  const step = await patchBugStep(reportId, bugId, stepId, payload);
  return mapStepResponse(step);
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
  return { bugId, steps: steps.map(mapStepResponse) };
});

export const createBugStepAttachmentFx = createEffect<
  { reportId: string; bugId: number; stepId: number; file: File },
  { bugId: number; stepId: number; attachment: Attachment }
>(async ({ reportId, bugId, stepId, file }) => {
  const result = await createBugStepAttachment(reportId, bugId, stepId, file);
  const attachment: Attachment = {
    id: result.id,
    entityId: result.entityId,
    attachType: result.attachType,
    createdAt: result.createdAt,
    creatorUserId: result.creatorUserId,
    fileName: result.fileName,
    hasPreview: result.hasPreview,
  };
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

// socket события
export const createBugStepSocketEvent = createEvent<BugStepResponse>();
export const patchBugStepSocketEvent = createEvent<{
  bugId: number;
  step: BugStepResponse;
}>();
export const deleteBugStepSocketEvent = createEvent<{
  bugId: number;
  stepId: number;
}>();
export const updateBugStepsOrderSocketEvent = createEvent<{
  bugId: number;
  steps: BugStepResponse[];
}>();
export const createBugStepAttachmentSocketEvent = createEvent<Attachment>();
export const bugStepAttachmentChangedSocketEvent = createEvent<Attachment>();
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
      updatedState[bugId] = sortSteps(steps.map(mapStepResponse));
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
  .on(createBugStepSocketEvent, (state, step) => {
    const mappedStep = mapStepResponse(step);
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

    const mappedStep = mapStepResponse({ ...step, bugId });

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
      const mapped = mapStepResponse(step);
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
  .on(createBugStepAttachmentSocketEvent, (state, attachment) => {
    if (attachment.attachType !== AttachmentTypes.BUG_STEP) return state;
    const stepId = attachment.entityId;

    let updatedState = state;

    for (const [bugId, steps] of Object.entries(state)) {
      const idx = steps.findIndex((step) => step.id === stepId);
      if (idx === -1) continue;

      const currentStep = steps[idx];
      const currentAttachments = currentStep.attachments || [];

      if (currentAttachments.some((a) => a.id === attachment.id)) {
        return state;
      }

      const updatedStep = {
        ...currentStep,
        attachments: [...currentAttachments, attachment],
      };

      updatedState = {
        ...state,
        [Number(bugId)]: [
          ...steps.slice(0, idx),
          updatedStep,
          ...steps.slice(idx + 1),
        ],
      };

      break;
    }

    return updatedState;
  })
  .on(bugStepAttachmentChangedSocketEvent, (state, attachment) => {
    if (attachment.attachType !== AttachmentTypes.BUG_STEP) return state;

    const stepId = attachment.entityId;
    let updatedState = state;

    for (const [bugId, steps] of Object.entries(state)) {
      const idx = steps.findIndex((step) => step.id === stepId);
      if (idx === -1) continue;

      const currentStep = steps[idx];
      const updatedStep = {
        ...currentStep,
        attachments: (currentStep.attachments || []).map((item) =>
          item.id === attachment.id ? { ...item, ...attachment } : item
        ),
      };

      updatedState = {
        ...state,
        [Number(bugId)]: [
          ...steps.slice(0, idx),
          updatedStep,
          ...steps.slice(idx + 1),
        ],
      };

      break;
    }

    return updatedState;
  })
  .on(deleteBugStepAttachmentSocketEvent, (state, { stepId, attachmentId }) => {
    let updatedState = state;

    for (const [bugId, steps] of Object.entries(state)) {
      const idx = steps.findIndex((step) => step.id === stepId);
      if (idx === -1) continue;

      const currentStep = steps[idx];
      const updatedStep = {
        ...currentStep,
        attachments: (currentStep.attachments || []).filter(
          (item) => item.id !== attachmentId
        ),
      };

      updatedState = {
        ...state,
        [Number(bugId)]: [
          ...steps.slice(0, idx),
          updatedStep,
          ...steps.slice(idx + 1),
        ],
      };

      break;
    }

    return updatedState;
  });

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

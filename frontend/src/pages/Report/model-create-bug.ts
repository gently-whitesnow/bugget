import { createEvent, createStore, sample } from "effector";

import { BugResultTypes } from "@/shared/config";
import type { ResultFieldTypes } from "@/entities/report";

export type NewBug = {
  title: string;
  receive: string;
  expect: string;
  reportId: string;
  clientId: number;
  isLocalOnly: boolean;
};

/**
 * События
 */
export const updateNewBugFieldEvent = createEvent<{
  clientId: number;
  field: ResultFieldTypes;
  value: string;
}>();

export const updateNewBugTitleEvent = createEvent<{
  clientId: number;
  title: string;
}>();

export const clearNewBugEvent = createEvent<void>();
export const createNewBugEvent = createEvent<{
  reportId: string;
  bugCount: number;
}>();

export const removeNewBugEvent = createEvent<{ clientId: number }>();

export const createBugOnServerEvent = createEvent<{
  reportId: string;
  receive: string;
  expect: string;
  clientId: number;
  title: string;
}>();

// Событие для создания бага по расфокусу
export const createBugOnBlurEvent = createEvent<{
  clientId: number;
  field: ResultFieldTypes;
  value: string;
}>();

// cобытие для управления фокусом
export const setFocusedBugEvent = createEvent<number>();

/** Сторы */

// Стор для одного локального бага
export const $newBugStore = createStore<NewBug | null>(null)
  .on(createNewBugEvent, (state, { reportId, bugCount }) => {
    // Если баг уже есть, не создаем новый
    if (state) return state;

    return {
      title: `Баг #${bugCount + 1}`,
      receive: "",
      expect: "",
      reportId,
      clientId: Date.now(),
      isLocalOnly: true,
    };
  })
  .on(updateNewBugFieldEvent, (state, { field, value }) => {
    if (!state) return null;
    return {
      ...state,
      [field]: value,
    };
  })
  .on(updateNewBugTitleEvent, (state, { clientId, title }) => {
    if (!state || state.clientId !== clientId) return state;
    return {
      ...state,
      title,
    };
  })
  .on(removeNewBugEvent, (state, { clientId }) => {
    if (state && state.clientId === clientId) return null;
    return state;
  })
  .reset(clearNewBugEvent);

export const $focusedBugClientId = createStore<number | null>(null)
  .on(setFocusedBugEvent, (_, clientId) => clientId)
  // Фокусируемся на созданном локальном баге
  .on(createNewBugEvent, () => {
    return null;
  });

/** Сэмплы */

// При создании бага сразу ставим фокус
sample({
  clock: $newBugStore,
  filter: (bug): bug is NewBug => !!bug,
  fn: (bug) => bug!.clientId,
  target: setFocusedBugEvent,
});

// Подготовка данных для создания бага по расфокусу
const $readyToCreateBugOnBlur = sample({
  clock: createBugOnBlurEvent,
  source: $newBugStore,
  filter: (state, { clientId }) =>
    state !== null && state.clientId === clientId && state.reportId !== null,
  fn: (state, { field, value }) => {
    if (!state) return null;

    const newReceive = field === BugResultTypes.RECEIVE ? value : state.receive;
    const newExpect = field === BugResultTypes.EXPECT ? value : state.expect;

    // Создаем баг только если есть хотя бы одно заполненное поле
    if (newReceive.trim() || newExpect.trim()) {
      return {
        reportId: state.reportId,
        receive: newReceive,
        expect: newExpect,
        clientId: state.clientId,
        title: state.title,
      };
    }
    return null;
  },
});

// Создание бага по расфокусу
sample({
  clock: $readyToCreateBugOnBlur,
  filter: (payload): payload is NonNullable<typeof payload> => payload !== null,
  target: createBugOnServerEvent,
});

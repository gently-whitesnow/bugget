import { createEffect, createStore } from "effector";
import {
  authorizationApi,
  authorizationPath,
} from "@/shared/api/instances/authorization";

export type Flags = {
  betaTest: boolean;
};

const defaultFlags: Flags = {
  betaTest: false,
};

export const fetchFlagsFx = createEffect<void, Flags>(async () => {
  const { data } = await authorizationApi.get<Flags>(
    authorizationPath("/flags")
  );
  return data;
});

export const $flags = createStore<Flags>(defaultFlags).on(
  fetchFlagsFx.doneData,
  (_, flags) => flags
);

export const $flagsReady = createStore(false)
  .on(fetchFlagsFx.done, () => true)
  .on(fetchFlagsFx.fail, () => true);

import { createStore, sample } from "effector";

import { fetchCurrentUserFx, $authUserStore } from "@/entities/user";

import {
  $userFilter,
  searchPageClosed,
  searchPageOpened,
  updateUserFilter,
} from "./model";

const $isSearchPageActive = createStore(false)
  .on(searchPageOpened, () => true)
  .on(searchPageClosed, () => false);

sample({
  source: $authUserStore,
  clock: searchPageOpened,
  filter: (user) => !!user?.id,
  fn: (user) => user.id,
  target: updateUserFilter,
});

sample({
  clock: fetchCurrentUserFx.doneData,
  source: { isActive: $isSearchPageActive, currentUserFilter: $userFilter },
  filter: ({ isActive, currentUserFilter }, user) =>
    isActive && !!user?.id && !currentUserFilter,
  fn: (_, user) => user.id,
  target: updateUserFilter,
});

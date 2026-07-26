import { combine } from "effector";

import { resolveCreatorName } from "./lib";
import { $creatorTypeStore, $creatorUserIdStore, $usersStore } from "./model";

// имя создателя отчёта — резолвится по creatorType через registry
export const $creatorUserNameStore = combine(
  $creatorUserIdStore,
  $creatorTypeStore,
  $usersStore,
  (creatorUserId, creatorType, users) =>
    resolveCreatorName(creatorUserId, creatorType, { users }) ?? ""
);

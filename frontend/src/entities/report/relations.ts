import { combine, sample } from "effector";

import {
  $externalUsersStore,
  fetchExternalUsersFx,
} from "@/entities/beta-test/@x/report";

import { isExternalCreatorType, resolveCreatorName } from "./lib";
import {
  $creatorTypeStore,
  $creatorUserIdStore,
  $usersStore,
  getReportFx,
} from "./model";

// имя создателя отчёта — резолвится по creatorType через registry
export const $creatorUserNameStore = combine(
  $creatorUserIdStore,
  $creatorTypeStore,
  $usersStore,
  $externalUsersStore,
  (creatorUserId, creatorType, users, externalUsers) =>
    resolveCreatorName(creatorUserId, creatorType, { users, externalUsers }) ??
    ""
);

// подтяжка external-user'а после загрузки репорта с внешним создателем
sample({
  clock: getReportFx.doneData,
  filter: (report) =>
    isExternalCreatorType(report.creatorType) && Boolean(report.creatorUserId),
  fn: (report) => [report.creatorUserId],
  target: fetchExternalUsersFx,
});

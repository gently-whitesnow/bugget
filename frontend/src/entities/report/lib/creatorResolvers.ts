import type { ExternalUserDto } from "@/entities/beta-test/@x/report";
import type { UserResponse } from "@/shared/api";
import { CreatorTypes } from "@/shared/config";

export type CreatorResolverContext = {
  users: Record<string, UserResponse>;
  externalUsers: Record<string, ExternalUserDto>;
};

export type CreatorResolver = (
  creatorUserId: string,
  ctx: CreatorResolverContext
) => string | null;

const internalUserResolver: CreatorResolver = (id, { users }) =>
  users[id]?.name ?? null;

const systemResolver: CreatorResolver = () => "Система";

const tgBetaTesterResolver: CreatorResolver = (id, { externalUsers }) =>
  externalUsers[id]?.displayName ?? null;

const resolvers: Partial<Record<CreatorTypes, CreatorResolver>> = {
  [CreatorTypes.USER]: internalUserResolver,
  [CreatorTypes.SYSTEM]: systemResolver,
  [CreatorTypes.TG_BETA_TESTER]: tgBetaTesterResolver,
};

export const resolveCreatorName = (
  creatorUserId: string,
  creatorType: number,
  ctx: CreatorResolverContext
): string | null => {
  if (!creatorUserId) return null;
  const resolver =
    resolvers[creatorType as CreatorTypes] ?? internalUserResolver;
  return resolver(creatorUserId, ctx);
};

export const isExternalCreatorType = (creatorType: number): boolean =>
  creatorType === CreatorTypes.TG_BETA_TESTER;

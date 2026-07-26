import type { UserResponse } from "@/shared/api";
import { CreatorTypes } from "@/shared/config";

export type CreatorResolverContext = {
  users: Record<string, UserResponse>;
};

export type CreatorResolver = (
  creatorUserId: string,
  ctx: CreatorResolverContext
) => string | null;

const internalUserResolver: CreatorResolver = (id, { users }) =>
  users[id]?.name ?? null;

const systemResolver: CreatorResolver = () => "Система";

const resolvers: Partial<Record<CreatorTypes, CreatorResolver>> = {
  [CreatorTypes.USER]: internalUserResolver,
  [CreatorTypes.SYSTEM]: systemResolver,
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

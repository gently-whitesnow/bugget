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

// Имя человека, выпустившего токен, в подписи агента не показывается —
// действие принадлежит агенту (kaiten 237718).
const agentResolver: CreatorResolver = () => "Агент";

const resolvers: Record<CreatorTypes, CreatorResolver> = {
  [CreatorTypes.USER]: internalUserResolver,
  [CreatorTypes.SYSTEM]: systemResolver,
  [CreatorTypes.TG_BETA_TESTER]: internalUserResolver,
  [CreatorTypes.AGENT]: agentResolver,
};

export const resolveCreatorName = (
  creatorUserId: string,
  creatorType: CreatorTypes,
  ctx: CreatorResolverContext
): string | null => {
  if (!creatorUserId) return null;
  const resolver: CreatorResolver | undefined = resolvers[creatorType];
  return resolver?.(creatorUserId, ctx) ?? null;
};

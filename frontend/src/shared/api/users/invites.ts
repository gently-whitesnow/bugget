import { usersApi, usersPath } from "@/shared/api/instances";
import type {
  AcceptInviteRequest,
  AcceptInviteResponse,
} from "@/shared/api/contracts";

export async function acceptTeamInvite(
  request: AcceptInviteRequest
): Promise<AcceptInviteResponse> {
  const { data } = await usersApi.post<AcceptInviteResponse>(
    usersPath("/invites/accept"),
    request
  );
  return data;
}

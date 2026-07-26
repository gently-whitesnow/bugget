import { usersApi, usersPathWithContext } from "@/shared/api/instances";
import type { UserResponse } from "@/shared/api/contracts";
import { mapUserResponse } from "./avatar";

export const fetchUsers = async (
  userIds: string[]
): Promise<UserResponse[]> => {
  if (userIds.length === 0) return [];

  const { data } = await usersApi.post<UserResponse[]>(
    usersPathWithContext("/users/batch/list"),
    userIds
  );
  return data.map(mapUserResponse);
};

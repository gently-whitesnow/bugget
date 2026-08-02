import { useUnit } from "effector-react";
import { $usersStore } from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import { CreatorTypes } from "@/shared/config";

const youString = "Вы";
const unknownUserString = "Пользователь";

export const useUserDisplayName = (
  commentUserId?: string,
  creatorType?: CreatorTypes
) => {
  const users = useUnit($usersStore);
  const currentUser = useUnit($authUserStore);

  if (creatorType === CreatorTypes.SYSTEM) return "Система";

  if (currentUser?.id && commentUserId === currentUser.id) return youString;

  if (commentUserId && users[commentUserId]?.name)
    return users[commentUserId].name;

  return unknownUserString;
};

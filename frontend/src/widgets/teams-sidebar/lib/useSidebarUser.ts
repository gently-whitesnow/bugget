import { useUnit } from "effector-react";
import { $authUserStore, isAdmin } from "@/entities/user";

export const useSidebarUser = () => {
  const authUser = useUnit($authUserStore);

  return {
    user: authUser,
    isAdmin: isAdmin(authUser),
  };
};

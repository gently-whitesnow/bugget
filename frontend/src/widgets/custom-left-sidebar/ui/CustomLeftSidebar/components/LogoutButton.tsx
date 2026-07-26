import { useUnit } from "effector-react";
import { logoutFx } from "@/entities/user";
import { getPostLogoutRedirectUrl } from "@/shared/lib/auth";

export const LogoutButton = () => {
  const [logoutPending, logout] = useUnit([logoutFx.pending, logoutFx]);

  const handleLogout = async () => {
    try {
      await logout();
      window.location.replace(getPostLogoutRedirectUrl());
    } catch (error) {
      console.error("Ошибка при выходе:", error);
    }
  };

  return (
    <button
      className="btn btn-sm btn-ghost text-xs opacity-60 hover:opacity-100 justify-start"
      onClick={handleLogout}
      disabled={logoutPending}
    >
      {logoutPending ? (
        <>
          <span className="loading loading-spinner loading-xs"></span>
          Выход...
        </>
      ) : (
        "Выйти из аккаунта"
      )}
    </button>
  );
};

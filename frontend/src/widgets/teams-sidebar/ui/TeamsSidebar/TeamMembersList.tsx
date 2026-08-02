import type { FC } from "react";
import { useUnit } from "effector-react";
import { UserX } from "lucide-react";

import {
  $deletingUserId,
  $isLoading,
  $memberDetails,
  deleteMember,
} from "../../model";
import { useSidebarUser } from "../../lib/useSidebarUser";

export const TeamMembersList: FC = () => {
  const members = useUnit($memberDetails);
  const isLoading = useUnit($isLoading);
  const deletingUserId = useUnit($deletingUserId);
  const removeMember = useUnit(deleteMember);
  const { user, isAdmin } = useSidebarUser();

  const handleDeleteMember = (userId: string, userName: string) => {
    const confirmed = confirm(
      `Вы уверены, что хотите удалить ${userName} из команды?`
    );
    if (!confirmed) return;

    removeMember({ userId, userName });
  };

  if (isLoading) {
    return <div className="text-xs text-base-content/50">Загрузка...</div>;
  }

  if (members.length === 0) {
    return <div className="text-xs text-base-content/50">Нет участников</div>;
  }

  return (
    <div className="flex flex-col gap-2">
      {members.map((member) => (
        <div key={member.id} className="flex items-center gap-2 text-xs group">
          <div className="flex items-center gap-2 flex-1">
            {member.imageUrl && (
              <img
                src={member.imageUrl}
                alt={member.name}
                className="w-6 h-6 rounded-full"
              />
            )}
            <span className="text-base-content/80">{member.name}</span>
          </div>
          {isAdmin && member.id !== user?.id && (
            <button
              onClick={() => handleDeleteMember(member.id, member.name)}
              disabled={deletingUserId !== null && deletingUserId === member.id}
              className="btn btn-xs btn-ghost text-error hover:bg-error/10 opacity-0 group-hover:opacity-100 transition-opacity"
              title="Удалить участника"
            >
              {deletingUserId === member.id ? (
                <span className="loading loading-spinner loading-xs"></span>
              ) : (
                <UserX className="w-3 h-3" />
              )}
            </button>
          )}
        </div>
      ))}
    </div>
  );
};

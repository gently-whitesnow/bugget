import { Crown } from "lucide-react";
import { useUnit } from "effector-react";

import { changeResponsibleUserIdEvent } from "@/entities/report";
import { Avatar } from "@/shared/ui";

type Props = {
  id: string;
  name: string;
  imageUrl?: string | null;
  isResponsible: boolean;
};

const ParticipantAvatar = ({ id, name, imageUrl, isResponsible }: Props) => {
  const changeResponsibleUser = useUnit(changeResponsibleUserIdEvent);

  return (
    <div
      className="tooltip tooltip-bottom relative cursor-pointer group"
      data-tip={name}
      onClick={() => !isResponsible && changeResponsibleUser(id)}
    >
      <Crown
        className={`w-3 h-3 absolute -top-3 left-1/2 -translate-x-1/2 ${
          isResponsible
            ? "text-warning"
            : "text-warning/0 group-hover:text-warning/70"
        } transition-colors`}
      />
      <Avatar src={imageUrl ?? undefined} />
    </div>
  );
};

export default ParticipantAvatar;

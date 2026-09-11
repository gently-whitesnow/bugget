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

  const tip = isResponsible
    ? `${name} — ответственный`
    : `${name} · назначить ответственным`;

  return (
    <div className="tooltip tooltip-bottom" data-tip={tip}>
      <button
        type="button"
        aria-label={tip}
        aria-pressed={isResponsible}
        className={`group relative block rounded-full transition-opacity duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/25 focus-visible:ring-offset-2 focus-visible:ring-offset-base-100 ${
          isResponsible
            ? "cursor-default ring-2 ring-primary"
            : "cursor-pointer opacity-70 hover:opacity-100"
        }`}
        onClick={() => !isResponsible && changeResponsibleUser(id)}
      >
        <Crown
          className={`w-3 h-3 absolute -top-3 left-1/2 -translate-x-1/2 text-primary transition-opacity duration-200 ${
            isResponsible
              ? "opacity-100"
              : "opacity-0 group-hover:opacity-60 group-focus-visible:opacity-60"
          }`}
        />
        <Avatar src={imageUrl ?? undefined} />
      </button>
    </div>
  );
};

export default ParticipantAvatar;

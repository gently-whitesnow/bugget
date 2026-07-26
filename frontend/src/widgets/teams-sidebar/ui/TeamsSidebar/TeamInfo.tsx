import type { FC } from "react";
import { useUnit } from "effector-react";

import { $membersCount } from "../../model";

type Props = {
  teamName: string;
};

export const TeamInfo: FC<Props> = ({ teamName }) => {
  const membersCount = useUnit($membersCount);

  const getMemberWord = (count: number) => {
    if (count === 1) return "участник";
    if (count >= 2 && count <= 4) return "участника";
    return "участников";
  };

  return (
    <div>
      <div className="text-sm font-semibold text-base-content/90">
        {teamName}
      </div>
      <div className="text-xs text-base-content/60 mt-1">
        {membersCount} {getMemberWord(membersCount)}
      </div>
    </div>
  );
};

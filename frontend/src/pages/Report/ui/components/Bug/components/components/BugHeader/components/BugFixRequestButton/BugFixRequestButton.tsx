import { useState } from "react";
import { Wrench } from "lucide-react";

import { useUnit } from "effector-react";
import { BugStatuses } from "@/shared/config";
import { $reportIdStore } from "@/entities/report";
import { requestBugFixFx } from "@/pages/Report/model-bug";

type Props = {
  bugId: number;
  status: BugStatuses;
};

/**
 * «Исправить баг» — попросить агента починить. Backend отвечает 202 и сам пишет
 * системный комментарий-маркер, который приходит по realtime: отдельного
 * индикатора успеха у кнопки нет намеренно.
 *
 * Дизейбл после клика держится до следующего изменения статуса бага: запомнен
 * статус на момент запроса, и как только `status` станет другим (руками или по
 * сокету), кнопка оживёт сама — без нового state-machine.
 */
const BugFixRequestButton = ({ bugId, status }: Props) => {
  const reportId = useUnit($reportIdStore);
  const requestFix = useUnit(requestBugFixFx);
  const [requestedAtStatus, setRequestedAtStatus] =
    useState<BugStatuses | null>(null);

  const isRequested = requestedAtStatus === status;

  const handleClick = () => {
    if (!reportId || isRequested) {
      return;
    }

    setRequestedAtStatus(status);
    requestFix({ reportId, bugId }).catch(() => {
      // Запрос не принят — кнопка обязана ожить сразу, а не ждать смены статуса.
      setRequestedAtStatus(null);
    });
  };

  return (
    <button
      onClick={handleClick}
      disabled={isRequested}
      className="btn btn-ghost btn-sm gap-1"
      title={
        isRequested
          ? "Исправление запрошено — агент оставит комментарий"
          : "Попросить агента исправить баг"
      }
    >
      <Wrench className="w-4 h-4" />
      Исправить баг
    </button>
  );
};

export default BugFixRequestButton;

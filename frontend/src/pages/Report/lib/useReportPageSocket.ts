import { useUnit } from "effector-react";
import { useEffect } from "react";
import { HubConnectionState } from "@microsoft/signalr";

import {
  $connection,
  $connectionId,
  joinReportFx,
  leaveReportFx,
} from "@/shared/model";
import { $initialReportStore } from "@/entities/report";

export const useReportPageSocket = () => {
  const [connection, connectionId, joinReport, leaveReport] = useUnit([
    $connection,
    $connectionId,
    joinReportFx,
    leaveReportFx,
  ]);
  const initialReport = useUnit($initialReportStore);
  const reportId = initialReport?.id ?? null;

  /**
   * При переподключении сервер выдаёт новый connectionId и забывает группы
   * соединения, поэтому join повторяется по connectionId, иначе события
   * репорта перестанут приходить.
   */
  useEffect(() => {
    if (!connection || !connectionId || reportId == null) return;

    joinReport({ conn: connection, reportId }).catch(console.error);

    return () => {
      // на мёртвом соединении invoke только бросит ошибку — сервер и так
      // вычистит группу вместе с соединением
      if (connection.state !== HubConnectionState.Connected) return;

      leaveReport({ conn: connection, reportId }).catch(console.error);
    };
  }, [connection, connectionId, reportId, joinReport, leaveReport]);
};

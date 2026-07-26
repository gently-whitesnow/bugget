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

// использование сокет соединения на странице репорта
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
   * Группа репорта — серверный список соединений, которым SignalR рассылает
   * события этого репорта (комментарии, баги и т.д.). Вступаем в неё заново
   * при каждой смене connectionId: при любом переподключении сервер выдаёт
   * новый connectionId и забывает, в каких группах мы состояли, — без
   * повторного join события репорта перестанут приходить. Поэтому эффект
   * перезапускается по connectionId, а не по факту «соединение есть».
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

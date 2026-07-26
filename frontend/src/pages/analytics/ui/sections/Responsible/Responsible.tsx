import { useEffect } from "react";
import { useUnit } from "effector-react";

import type { AnalyticsPeriod } from "@/shared/lib/time";

import {
  $responsibleStore,
  $responsibleError,
  $selectedUserPreview,
  fetchResponsibleFx,
  responsibleMounted,
  responsibleUnmounted,
  periodChanged,
  userIdChanged,
  userSelected,
} from "../../../model/responsible";
import UserSelector from "./components/UserSelector";
import ParticipatedReports from "./components/ParticipatedReports";
import CompletedReports from "./components/CompletedReports";
import AvgFixCard from "./components/AvgFixCard";

type Props = {
  period: AnalyticsPeriod;
  userId: string | null;
  onUserChange: (userId: string | null) => void;
};

const AnalyticsResponsible = ({ period, userId, onUserChange }: Props) => {
  const [
    responsible,
    isPending,
    error,
    selectedUser,
    onMounted,
    onUnmounted,
    onPeriodChanged,
    onUserIdChanged,
    onUserSelected,
  ] = useUnit([
    $responsibleStore,
    fetchResponsibleFx.pending,
    $responsibleError,
    $selectedUserPreview,
    responsibleMounted,
    responsibleUnmounted,
    periodChanged,
    userIdChanged,
    userSelected,
  ]);

  // Sync props → model.
  useEffect(() => {
    onPeriodChanged(period);
  }, [period, onPeriodChanged]);

  useEffect(() => {
    onUserIdChanged(userId);
  }, [userId, onUserIdChanged]);

  useEffect(() => {
    onMounted();
    return () => {
      onUnmounted();
    };
  }, [onMounted, onUnmounted]);

  const noUserSelected = !userId;

  const handleUserChange = (
    user: { id: string; name: string; imageUrl?: string } | null
  ) => {
    onUserSelected(user);
    onUserChange(user?.id ?? null);
  };

  return (
    <div className="flex flex-col gap-4">
      <UserSelector
        selectedName={selectedUser?.name ?? ""}
        selectedImageUrl={selectedUser?.imageUrl}
        onUserChange={handleUserChange}
      />

      {noUserSelected ? (
        <div className="rounded-md border border-base-300 bg-base-100 p-6 text-sm text-base-content/60">
          Выберите пользователя, чтобы посмотреть аналитику ответственного.
        </div>
      ) : isPending && !responsible ? (
        <div className="py-12 flex items-center justify-center">
          <span className="loading loading-spinner loading-md"></span>
        </div>
      ) : error && !responsible ? (
        <div className="rounded-md border border-error/30 bg-error/5 p-4 text-sm text-error">
          Не удалось загрузить аналитику пользователя: {error}
        </div>
      ) : (
        <>
          <AvgFixCard
            avgFixPhaseDays={responsible?.avg_fix_phase_days ?? null}
          />

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
            <ParticipatedReports
              reports={responsible?.reports_participated ?? []}
            />
            <CompletedReports reports={responsible?.reports_completed ?? []} />
          </div>
        </>
      )}
    </div>
  );
};

export default AnalyticsResponsible;

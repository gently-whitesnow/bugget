import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useUnit } from "effector-react";

import { resolveLegacyReport } from "@/entities/report";
import { $bootstrapState } from "@/shared/model";
import { setAppContext } from "@/shared/api";
import { BootstrapStatus } from "@/shared/config";

const LegacyReportRedirectPage = () => {
  const { legacyId } = useParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const bootstrapState = useUnit($bootstrapState);

  useEffect(() => {
    if (!legacyId) {
      setError("missing");
      return;
    }

    // URL /reports/:legacyId не содержит workspace/team,
    // поэтому ApiBaseBoot не может извлечь контекст из пути.
    // Ждём bootstrap и устанавливаем контекст, чтобы appApi сформировал правильный URL.
    if (
      bootstrapState.status !== BootstrapStatus.READY ||
      !bootstrapState.workspace
    )
      return;

    if (!bootstrapState.defaultTeamId) {
      setError("not-found");
      return;
    }

    setAppContext(bootstrapState.workspace.id, bootstrapState.defaultTeamId);

    resolveLegacyReport(legacyId)
      .then(({ teamId, teamReportId }) => {
        navigate(`/teams/${teamId}/reports/${teamReportId}`, { replace: true });
      })
      .catch(() => setError("not-found"));
  }, [legacyId, navigate, bootstrapState]);

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="text-lg font-semibold">Репорт не найден</div>
          <div className="text-sm opacity-70 mt-1">
            Проверьте ссылку или откройте список репортов.
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="loading loading-spinner loading-lg"></div>
    </div>
  );
};

export default LegacyReportRedirectPage;

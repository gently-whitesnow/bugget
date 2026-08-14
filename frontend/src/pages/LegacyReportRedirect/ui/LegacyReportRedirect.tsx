import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useUnit } from "effector-react";

import { resolveLegacyReport } from "@/entities/report";
import { $bootstrapState } from "@/shared/model";
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

    if (
      bootstrapState.status !== BootstrapStatus.READY ||
      !bootstrapState.workspace
    )
      return;

    if (!bootstrapState.defaultTeamId) {
      setError("not-found");
      return;
    }

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

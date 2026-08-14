import { useLayoutEffect } from "react";
import { useLocation } from "react-router";
import { useUnit } from "effector-react";
import { parseAppContextFromPath, setAppContext } from "@/shared/api";
import { BootstrapStatus } from "@/shared/config";
import { $bootstrapState } from "@/shared/model";

export default function ApiBaseBoot() {
  const location = useLocation();
  const bootstrapState = useUnit($bootstrapState);

  useLayoutEffect(() => {
    const fromPath = parseAppContextFromPath(location.pathname);
    if (fromPath.workspaceId && fromPath.teamId) {
      setAppContext(fromPath.workspaceId, fromPath.teamId);
      return;
    }

    if (bootstrapState.status === BootstrapStatus.READY) {
      setAppContext(bootstrapState.workspace.id, bootstrapState.defaultTeamId);
      return;
    }

    setAppContext(null, null);
  }, [bootstrapState, location.pathname]);

  return null;
}

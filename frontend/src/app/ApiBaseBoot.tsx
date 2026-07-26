import { useLayoutEffect } from "react";
import { useLocation } from "react-router-dom";
import { parseAppContextFromPath, setAppContext } from "@/shared/api";

export default function ApiBaseBoot() {
  const location = useLocation();

  useLayoutEffect(() => {
    const { workspaceId, teamId } = parseAppContextFromPath(location.pathname);
    setAppContext(workspaceId, teamId);
  }, [location.pathname]);

  return null;
}

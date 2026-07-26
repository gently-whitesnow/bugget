import { sample } from "effector";
import { $bootstrapState } from "@/shared/model";
import { loadUserEvent } from "@/entities/user";
import { BootstrapStatus } from "@/shared/config";

/**
 * После успешного bootstrap (status: 'ready') загружаем текущего пользователя
 */
sample({
  source: $bootstrapState,
  filter: (state) => state.status === BootstrapStatus.READY,
  fn: (state) => {
    if (state.status !== BootstrapStatus.READY) {
      throw new Error("Unexpected state");
    }
    return {
      workspaceId: state.workspace.id,
      teamId: state.defaultTeamId,
    };
  },
  target: loadUserEvent,
});

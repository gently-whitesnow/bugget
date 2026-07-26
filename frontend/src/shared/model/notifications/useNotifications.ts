import { useUnit } from "effector-react";
import {
  $degradedModeMessage,
  $notifications,
  clearAllRequested,
  clearDegradedModeRequested,
  dismissRequested,
  notifyErrorRequested,
  notifyRequested,
  notifySuccessRequested,
  setDegradedModeRequested,
} from "./model";
import { NotifyErrorOptions } from "./types";

export const useNotifications = () => {
  const [
    notifications,
    degradedModeMessage,
    notifyRequestedFn,
    dismissRequestedFn,
    clearAllRequestedFn,
    notifySuccessRequestedFn,
    notifyErrorRequestedFn,
    setDegradedModeRequestedFn,
    clearDegradedModeRequestedFn,
  ] = useUnit([
    $notifications,
    $degradedModeMessage,
    notifyRequested,
    dismissRequested,
    clearAllRequested,
    notifySuccessRequested,
    notifyErrorRequested,
    setDegradedModeRequested,
    clearDegradedModeRequested,
  ]);

  const notifySuccess = (title: string, message?: string) => {
    notifySuccessRequestedFn({ title, message });
  };

  const notifyError = (
    title: string,
    message?: string,
    options?: NotifyErrorOptions
  ) => {
    notifyErrorRequestedFn({ title, message, options });
  };

  return {
    notifications,
    degradedModeMessage,
    notify: notifyRequestedFn,
    dismiss: dismissRequestedFn,
    clearAll: clearAllRequestedFn,
    notifySuccess,
    notifyError,
    setDegradedMode: setDegradedModeRequestedFn,
    clearDegradedMode: clearDegradedModeRequestedFn,
  };
};

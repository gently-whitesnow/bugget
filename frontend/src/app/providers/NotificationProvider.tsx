import { ReactNode } from "react";
import { NotificationToaster, SystemBanner } from "@/shared/ui";

type NotificationProviderProps = {
  children: ReactNode;
};

export const NotificationProvider = ({
  children,
}: NotificationProviderProps) => {
  return (
    <div className="flex h-dvh flex-col">
      <SystemBanner />
      <div className="min-h-0 flex-1 overflow-auto">{children}</div>
      <NotificationToaster />
    </div>
  );
};

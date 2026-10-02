import type { LucideIcon } from "lucide-react";

export type StatusOption<T> = {
  value: T;
  label: string;
  description?: string;
  icon: LucideIcon;
  iconClassName: string;
  activeClassName: string;
};

import { Outlet } from "react-router-dom";
import type { ReactNode } from "react";

import { Layout } from "@/widgets/layout";

type Props = {
  leftSidebar?: ReactNode;
  rightSidebar?: ReactNode;
  header?: ReactNode;
};

export const AppLayout = ({ leftSidebar, rightSidebar, header }: Props) => (
  <Layout leftSidebar={leftSidebar} rightSidebar={rightSidebar} header={header}>
    <Outlet />
  </Layout>
);

export const ReportLayout = ({ rightSidebar, header }: Props) => (
  <Layout rightSidebar={rightSidebar} header={header}>
    <Outlet />
  </Layout>
);

export const SetupLayout = ({ header }: Props) => (
  <Layout header={header}>
    <Outlet />
  </Layout>
);

export const SearchLayout = ({ header }: Props) => (
  <Layout header={header}>
    <Outlet />
  </Layout>
);

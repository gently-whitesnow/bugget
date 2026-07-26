import { useEffect } from "react";
import { useUnit } from "effector-react";
import {
  dashboardPageOpened,
  setRecentlyResolvedSectionOpened,
} from "@/entities/dashboard";
import {
  $recentlyResolvedReports,
  $responsibleReports,
  $participantReports,
} from "../relations";
import { $isDashboardReportsLoaded } from "@/entities/report-list";
import Section from "./components/Section";
import LastReportsSection from "./components/LastReportsSection";
import DashboardNotActiveState from "./components/DashboardNotActiveState";

const DashboardContent = () => {
  const responsibleReports = useUnit($responsibleReports);
  const participantReports = useUnit($participantReports);
  const recentlyResolvedReports = useUnit($recentlyResolvedReports);
  const isDashboardLoaded = useUnit($isDashboardReportsLoaded);
  const [openDashboardPage, setResolvedSectionOpened] = useUnit([
    dashboardPageOpened,
    setRecentlyResolvedSectionOpened,
  ]);

  useEffect(() => {
    openDashboardPage();
  }, [openDashboardPage]);

  const showResponsibleSection = responsibleReports.length > 0;
  const showParticipantSection = participantReports.length > 0;
  const noActiveSections =
    isDashboardLoaded && !showResponsibleSection && !showParticipantSection;

  return (
    <>
      {noActiveSections && <DashboardNotActiveState />}
      {showResponsibleSection && (
        <Section
          title="Ответственный"
          reports={responsibleReports}
          className="border-error"
        />
      )}
      {showParticipantSection && (
        <Section title="Участник" reports={participantReports} />
      )}
      <LastReportsSection
        data={recentlyResolvedReports}
        onExpand={setResolvedSectionOpened}
        defaultExpanded={noActiveSections}
      />
    </>
  );
};

export default DashboardContent;

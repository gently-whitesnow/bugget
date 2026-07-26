import { useNavigate, useParams } from "react-router";
import { useUnit } from "effector-react";
import { clearReport } from "@/entities/report";

type Props = {
  label?: string;
  className?: string;
};

const CreateReportButton = ({
  label = "Новый репорт",
  className = "btn btn-primary font-normal",
}: Props) => {
  const navigate = useNavigate();
  const { teamId } = useParams();
  const clearReportFn = useUnit(clearReport);

  const handleCreateReport = () => {
    clearReportFn();

    // /teams/:teamId/reports
    if (teamId) {
      navigate(`/teams/${teamId}/reports`);
      return;
    }

    // Fallback
    navigate("/reports");
  };

  return (
    <button className={className} onClick={handleCreateReport}>
      {label}
    </button>
  );
};

export default CreateReportButton;

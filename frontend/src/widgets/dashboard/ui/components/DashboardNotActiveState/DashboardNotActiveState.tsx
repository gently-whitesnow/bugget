import { CreateReportButton } from "@/features/create-report";

const DashboardNotActiveState = () => {
  return (
    <div className="flex flex-col items-center justify-center gap-4 py-12 text-center">
      <p className="text-base-content/70">Нет активных репортов</p>
      <CreateReportButton label="Создать новый репорт" />
    </div>
  );
};

export default DashboardNotActiveState;

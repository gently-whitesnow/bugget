import type { AnalyticsSection } from "../../lib/section";

type Props = {
  value: AnalyticsSection;
  onChange: (next: AnalyticsSection) => void;
};

const tabs: { key: AnalyticsSection; label: string }[] = [
  { key: "overview", label: "Все" },
  { key: "team", label: "Команда" },
  { key: "responsible", label: "Ответственный" },
  { key: "report", label: "Репорт" },
];

const CrossSectionTabs = ({ value, onChange }: Props) => {
  return (
    <div role="tablist" className="tabs tabs-boxed">
      {tabs.map((t) => (
        <button
          key={t.key}
          type="button"
          role="tab"
          aria-selected={value === t.key}
          className={`tab ${value === t.key ? "tab-active" : ""}`}
          onClick={() => onChange(t.key)}
        >
          {t.label}
        </button>
      ))}
    </div>
  );
};

export default CrossSectionTabs;

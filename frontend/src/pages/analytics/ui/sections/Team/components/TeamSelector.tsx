import type { TeamResponse } from "@/shared/api";

type Props = {
  teams: TeamResponse[];
  value: string | null;
  onChange: (teamId: string) => void;
};

const TeamSelector = ({ teams, value, onChange }: Props) => {
  if (teams.length === 0) return null;

  return (
    <label className="flex items-center gap-2 text-sm">
      <span className="text-base-content/60">Команда:</span>
      <select
        className="select select-sm select-bordered"
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="" disabled>
          Выберите команду
        </option>
        {teams.map((t) => (
          <option key={t.id} value={t.id}>
            {t.name}
          </option>
        ))}
      </select>
    </label>
  );
};

export default TeamSelector;

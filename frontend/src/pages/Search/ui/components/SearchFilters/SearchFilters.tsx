import { ReportStatusSelect } from "@/entities/report";
import { autocompleteUsersForAutosuggest } from "@/entities/user";
import { teamsAutocomplete } from "@/shared/api";
import { Autosuggest } from "@/shared/ui";
import {
  updateStatuses,
  $statuses,
  $userFilter,
  $usersStore,
  updateUserFilter,
  $teamFilter,
  updateTeamFilter,
} from "../../../model";
import { Team } from "@/shared/api";
import { useUnit } from "effector-react";

const autocompleteTeams = async (searchString: string) => {
  const response = await teamsAutocomplete(searchString);
  return (response.teams ?? [])
    .filter(
      (team: Team): team is Team & { id: string; name: string } =>
        !!team.id && !!team.name
    )
    .map((team) => ({
      id: team.id,
      display: team.name,
    }));
};

const SearchFilters = () => {
  const searchFilterActions = useUnit({
    updateStatuses,
    updateUserFilter,
    updateTeamFilter,
  });
  const [statuses, userFilter, usersStore, teamFilter] = useUnit([
    $statuses,
    $userFilter,
    $usersStore,
    $teamFilter,
  ]);

  const userFilterUser = userFilter ? usersStore[userFilter] : undefined;
  const userFilterName = userFilterUser?.name || "";
  const userFilterImageUrl = userFilterUser?.imageUrl;

  return (
    <aside className="search-layout-filters responsive-sidebar h-fit rounded-2xl border border-base-300 bg-base-100 p-4">
      <div className="mb-2 text-lg font-medium">Поисковые фильтры</div>

      <div className="flex flex-col gap-2">
        <div className="flex flex-col gap-1.5">
          <ReportStatusSelect
            multiple
            value={statuses}
            onChange={searchFilterActions.updateStatuses}
            className="w-full"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <div className="field-label">Участник</div>
          <Autosuggest
            onSelect={(entity) =>
              searchFilterActions.updateUserFilter(entity ? entity.id : null)
            }
            externalString={userFilterName}
            externalImageUrl={userFilterImageUrl}
            autocompleteFn={autocompleteUsersForAutosuggest}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <div className="field-label">Команда</div>
          <Autosuggest
            onSelect={(entity) =>
              searchFilterActions.updateTeamFilter(
                entity ? { id: entity.id, name: entity.display } : null
              )
            }
            externalString={teamFilter?.name || ""}
            autocompleteFn={autocompleteTeams}
          />
        </div>
      </div>
    </aside>
  );
};

export default SearchFilters;

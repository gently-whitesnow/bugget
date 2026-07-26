import { Autosuggest } from "@/shared/ui";
import { autocompleteUsersForAutosuggest } from "@/entities/user";

type Props = {
  selectedName: string;
  selectedImageUrl?: string | null;
  onUserChange: (
    user: { id: string; name: string; imageUrl?: string } | null
  ) => void;
};

const UserSelector = ({
  selectedName,
  selectedImageUrl,
  onUserChange,
}: Props) => {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="text-base-content/60 text-sm">Ответственный</div>
      <Autosuggest
        onSelect={(entity) =>
          onUserChange(
            entity
              ? {
                  id: entity.id,
                  name: entity.display,
                  imageUrl: entity.imageUrl,
                }
              : null
          )
        }
        externalString={selectedName}
        externalImageUrl={selectedImageUrl}
        autocompleteFn={autocompleteUsersForAutosuggest}
      />
    </div>
  );
};

export default UserSelector;

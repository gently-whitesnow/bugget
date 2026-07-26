import { autocompleteUsersForAutosuggest } from "@/entities/user";
import AnalyticsMenu from "./AnalyticsMenu";
import { Autosuggest } from "@/shared/ui";
import ParticipantAvatar from "./ParticipantAvatar";
import ReportStatusSelect from "./ReportStatusSelect";
import ReturnReportButton from "./ReturnReportButton";
import BugStatusStats from "./BugStatusStats";

type Participant = {
  id: string;
  name: string;
  imageUrl?: string | null;
};

type Props = {
  responsibleUserName: string;
  responsibleUserId: string | null;
  responsibleUserImageUrl?: string | null;
  participantsWithNames: Participant[] | null;
  shouldShowResponsibleLinkButton: boolean;
  isCopied: boolean;
  onResponsibleUserChange: (userId: string | null) => void;
  onCopyResponsibleLink: () => void;
  excludeReportId: number | null;
  isExcludedFromAnalytics: boolean;
  onIsExcludedChange: (value: boolean) => void;
};

const Content = ({
  responsibleUserName,
  responsibleUserId,
  responsibleUserImageUrl,
  participantsWithNames,
  shouldShowResponsibleLinkButton,
  isCopied,
  onResponsibleUserChange,
  onCopyResponsibleLink,
  excludeReportId,
  isExcludedFromAnalytics,
  onIsExcludedChange,
}: Props) => {
  return (
    <>
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <div className="text-sm text-base-content/70">Статус</div>
            {excludeReportId !== null && (
              <AnalyticsMenu
                reportId={excludeReportId}
                value={isExcludedFromAnalytics}
                onChange={onIsExcludedChange}
              />
            )}
          </div>
          <ReportStatusSelect />
          <BugStatusStats />
        </div>

        {
          <>
            <div className="flex flex-col gap-2">
              <div className="text-sm text-base-content/70">Ответственный</div>
              <Autosuggest
                onSelect={(entity) => {
                  onResponsibleUserChange(entity ? entity.id : null);
                }}
                externalString={responsibleUserName}
                externalImageUrl={responsibleUserImageUrl}
                autocompleteFn={autocompleteUsersForAutosuggest}
              />
              {shouldShowResponsibleLinkButton && (
                <button
                  onClick={onCopyResponsibleLink}
                  className="btn btn-sm btn-primary w-full"
                >
                  {isCopied ? "✓ Скопировано" : "Ссылка стать ответственным"}
                </button>
              )}
            </div>

            <div className="flex flex-col gap-2">
              <div className="text-sm text-base-content/70">Участники</div>
              <div className="mt-2 flex gap-2">
                {participantsWithNames?.map((participant) => (
                  <ParticipantAvatar
                    key={participant.id}
                    id={participant.id}
                    name={participant.name}
                    imageUrl={participant.imageUrl}
                    isResponsible={participant.id === responsibleUserId}
                  />
                ))}
                {(!participantsWithNames ||
                  participantsWithNames.length === 0) && (
                  <span className="text-sm text-base-content/50">
                    Нет участников
                  </span>
                )}
              </div>
            </div>
          </>
        }
      </div>

      <ReturnReportButton />
    </>
  );
};

export default Content;

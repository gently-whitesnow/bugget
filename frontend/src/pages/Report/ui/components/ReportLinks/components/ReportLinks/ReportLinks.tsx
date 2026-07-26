import { useState } from "react";
import { useUnit } from "effector-react";
import { Plus } from "lucide-react";
import {
  $reportLinksStore,
  createLinkEvent,
  updateLinkEvent,
  deleteLinkEvent,
} from "@/pages/Report/model-report-link";
import { ReportLinkDto } from "@/entities/report";
import LinkChip from "./components/LinkChip";
import LinkForm from "./components/LinkForm";

const ReportLinks = () => {
  const links = useUnit($reportLinksStore);
  const reportLinkActions = useUnit({
    createLinkEvent,
    updateLinkEvent,
    deleteLinkEvent,
  });
  const [isAdding, setIsAdding] = useState(false);
  const [editingLinkId, setEditingLinkId] = useState<number | null>(null);

  const handleAdd = (dto: ReportLinkDto) => {
    reportLinkActions.createLinkEvent(dto);
    setIsAdding(false);
  };

  const handleUpdate = (linkId: number) => (dto: ReportLinkDto) => {
    reportLinkActions.updateLinkEvent({ linkId, dto });
    setEditingLinkId(null);
  };

  const handleEdit = (linkId: number) => {
    setEditingLinkId(linkId);
    setIsAdding(false);
  };

  const handleStartAdding = () => {
    setIsAdding(true);
    setEditingLinkId(null);
  };

  const isEmpty = links.length === 0;

  return (
    <div className="flex flex-wrap items-center gap-2">
      {links.map((link) => (
        <div key={link.id} className="relative">
          <LinkChip
            link={link}
            onEdit={() => handleEdit(link.id)}
            onDelete={() => reportLinkActions.deleteLinkEvent(link.id)}
          />
          {editingLinkId === link.id && (
            <LinkForm
              initialValues={{ name: link.name, link: link.link }}
              onSave={handleUpdate(link.id)}
              onCancel={() => setEditingLinkId(null)}
            />
          )}
        </div>
      ))}

      <div className="relative">
        {isEmpty && !isAdding ? (
          <button
            className="flex items-center gap-1 text-base-content/70 hover:text-base-content cursor-pointer transition-colors hover:bg-base-200 rounded-lg p-2"
            onClick={handleStartAdding}
          >
            <Plus className="w-4 h-4" />
            <span className="text-sm">добавить ссылку</span>
          </button>
        ) : !isAdding ? (
          <button
            className="btn btn-square btn-sm btn-ghost bg-base-200 hover:bg-base-300"
            onClick={handleStartAdding}
            title="Добавить ссылку"
          >
            <Plus className="w-4 h-4 text-base-content/70" />
          </button>
        ) : null}

        {isAdding && (
          <LinkForm onSave={handleAdd} onCancel={() => setIsAdding(false)} />
        )}
      </div>
    </div>
  );
};

export default ReportLinks;

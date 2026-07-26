import { useEffect, useRef } from "react";

import { useStoreMap, useUnit } from "effector-react";

import {
  BugResultTypes,
  BugStatuses,
  AttachmentTypes,
  bugStatusMap,
} from "@/shared/config";
import {
  deleteAttachmentEvent,
  renameAttachmentFx,
  uploadAttachmentFx,
  $attachmentsData,
} from "../../../../model-attachment";
import { updateBugDataEvent } from "@/pages/Report/model-bug";
import { $reportIdStore } from "@/entities/report";
import {
  $focusedBugClientId,
  $newBugStore,
  createBugOnBlurEvent,
  updateNewBugFieldEvent,
} from "@/pages/Report/model-create-bug";
import { getBugElementId } from "../../../../lib";
import {
  BugClientEntity,
  BugFormData,
  ResultFieldTypes,
} from "@/entities/report";

import BugHeader from "./components/BugHeader/BugHeader";
import Result from "./components/Result/Result";
import Comments from "./components/Comments/Comments";
import BugSteps from "./components/BugSteps/BugSteps";

import "./Bug.css";

type Props = {
  bug: BugClientEntity;
  totalBugsCount: number;
};

const Bug = ({ bug, totalBugsCount }: Props) => {
  const reportId = useUnit($reportIdStore);
  const newBug = useUnit($newBugStore);
  const focusedClientId = useUnit($focusedBugClientId);
  const bugActions = useUnit({
    updateBugDataEvent,
    updateNewBugFieldEvent,
    createBugOnBlurEvent,
    uploadAttachmentFx,
    deleteAttachmentEvent,
    renameAttachmentFx,
  });
  const receiveTextareaRef = useRef<HTMLDivElement>(null);
  const expectTextareaRef = useRef<HTMLDivElement>(null);
  const statusMeta = bugStatusMap[bug.status];

  const allAttachments = useStoreMap({
    store: $attachmentsData,
    keys: [bug.id],
    fn: ({ attachments, bugAttachments }, [bugId]) => {
      if (!bugId) return [];
      const attachmentIds = bugAttachments[bugId] || [];
      return attachmentIds.map((id) => attachments[id]).filter(Boolean);
    },
  });

  const adjustTextareaHeights = () => {
    const receiveTextarea = receiveTextareaRef.current;
    const expectTextarea = expectTextareaRef.current;

    if (receiveTextarea && expectTextarea) {
      receiveTextarea.style.height = "auto";
      expectTextarea.style.height = "auto";

      const receiveHeight = receiveTextarea.scrollHeight;
      const expectHeight = expectTextarea.scrollHeight;

      const maxHeight = Math.max(receiveHeight, expectHeight);

      receiveTextarea.style.height = `${maxHeight}px`;
      expectTextarea.style.height = `${maxHeight}px`;
    }
  };

  // Синхронизация высоты текстовых полей
  useEffect(() => {
    adjustTextareaHeights();
  }, [bug.receive, bug.expect, newBug?.receive, newBug?.expect]);

  const handleReceiveInput = (value: string) => {
    adjustTextareaHeights();
    if (bug.isLocalOnly) {
      handleTemporaryBugChange(BugResultTypes.RECEIVE, value);
    }
  };

  const handleExpectInput = (value: string) => {
    adjustTextareaHeights();
    if (bug.isLocalOnly) {
      handleTemporaryBugChange(BugResultTypes.EXPECT, value);
    }
  };

  const receiveAttachments = allAttachments.filter(
    (att) => att.attachType === AttachmentTypes.FACT
  );
  const expectAttachments = allAttachments.filter(
    (att) => att.attachType === AttachmentTypes.EXPECT
  );

  const updateBugFields = (bugId: number, data: Partial<BugFormData>) => {
    if (!reportId) return;
    bugActions.updateBugDataEvent({ bugId, reportId, data });
  };

  const handleTemporaryBugChange = (field: ResultFieldTypes, value: string) => {
    bugActions.updateNewBugFieldEvent({ clientId: bug.clientId, field, value });
  };

  const handleExistingBugChange = (field: ResultFieldTypes, value: string) => {
    const data: Partial<BugFormData> = {};
    if (value.trim()) {
      data[field] = value.trim();
    }
    updateBugFields(bug.id, data);
  };

  const handleReceiveBlur = (value: string) => {
    if (bug.isLocalOnly) {
      bugActions.createBugOnBlurEvent({
        clientId: bug.clientId,
        field: BugResultTypes.RECEIVE,
        value,
      });
    } else {
      handleExistingBugChange(BugResultTypes.RECEIVE, value);
    }
  };

  const handleExpectBlur = (value: string) => {
    if (bug.isLocalOnly) {
      bugActions.createBugOnBlurEvent({
        clientId: bug.clientId,
        field: BugResultTypes.EXPECT,
        value,
      });
    } else {
      handleExistingBugChange(BugResultTypes.EXPECT, value);
    }
  };

  const handleStatusChange = (status: BugStatuses) => {
    updateBugFields(bug.id, { status });
  };

  const handleTitleChange = (title: string) => {
    updateBugFields(bug.id, { title });
  };

  const handleAttachmentUpload =
    (attachType: AttachmentTypes) => (file: File) => {
      if (!reportId || bug.isLocalOnly) return Promise.resolve();

      return bugActions.uploadAttachmentFx({
        reportId,
        bugId: bug.id,
        attachType,
        file,
      });
    };

  const handleDeleteAttachment = (attachmentId: number) => {
    if (!reportId || bug.isLocalOnly) return;

    bugActions.deleteAttachmentEvent({
      reportId,
      bugId: bug.id,
      attachmentId,
    });
  };

  const handleRenameAttachment = (attachmentId: number, fileName: string) => {
    if (!reportId || bug.isLocalOnly) return Promise.resolve();

    return bugActions.renameAttachmentFx({
      reportId,
      bugId: bug.id,
      attachmentId,
      fileName,
    });
  };

  return (
    <div
      id={bug.isLocalOnly ? undefined : getBugElementId(bug.id)}
      className={`bug-card card mb-4 grid grid-cols-1 gap-4 border border-base-300 bg-base-100 p-4 shadow-lg ${statusMeta.borderColor}`}
    >
      <BugHeader
        bug={bug}
        onStatusChange={handleStatusChange}
        onTitleChange={handleTitleChange}
        isFirstBug={totalBugsCount === 1}
      />

      <Result
        ref={receiveTextareaRef}
        title="фактический результат"
        value={bug.isLocalOnly ? newBug?.receive || "" : bug.receive || ""}
        onBlur={handleReceiveBlur}
        colorType="error"
        autoFocus={bug.clientId === focusedClientId}
        attachments={receiveAttachments}
        reportId={reportId}
        bugId={bug.id}
        attachType={AttachmentTypes.FACT}
        onAttachmentUpload={handleAttachmentUpload(AttachmentTypes.FACT)}
        onAttachmentDelete={handleDeleteAttachment}
        onAttachmentRename={handleRenameAttachment}
        onInput={handleReceiveInput}
        disabled={bug.isLocalOnly}
      />

      <Result
        ref={expectTextareaRef}
        title="ожидаемый результат"
        value={bug.isLocalOnly ? newBug?.expect || "" : bug.expect || ""}
        onBlur={handleExpectBlur}
        colorType="success"
        autoFocus={false}
        attachments={expectAttachments}
        reportId={reportId}
        bugId={bug.id}
        attachType={AttachmentTypes.EXPECT}
        onAttachmentUpload={handleAttachmentUpload(AttachmentTypes.EXPECT)}
        onAttachmentDelete={handleDeleteAttachment}
        onAttachmentRename={handleRenameAttachment}
        onInput={handleExpectInput}
        disabled={bug.isLocalOnly}
      />

      <div className="bug-card-actions flex flex-wrap items-start gap-2">
        <BugSteps
          reportId={reportId}
          bugId={bug.id}
          disabled={bug.isLocalOnly && !newBug?.receive && !newBug?.expect}
          resolved={bug.status !== BugStatuses.OPEN}
        />
        <Comments
          reportId={reportId!}
          bugId={bug.id}
          disabled={bug.isLocalOnly && !newBug?.receive && !newBug?.expect}
          resolved={
            bug.status !== BugStatuses.OPEN && bug.status !== BugStatuses.FIXED
          }
        />
      </div>
    </div>
  );
};

export default Bug;

import { useUnit } from "effector-react";

import {
  bugAttachmentCreatedSocketEvent,
  bugAttachmentDeletedSocketEvent,
  bugAttachmentChangedSocketEvent,
} from "../model-attachment";
import {
  createBugSocketEvent,
  isBugStepAttachment,
  isCommentAttachment,
  patchBugSocketEvent,
} from "@/entities/report";
import {
  addParticipantSocketEvent,
  patchReportSocketEvent,
  $reportIdStore,
} from "@/entities/report";
import {
  createBugStepAttachmentSocketEvent,
  createBugStepSocketEvent,
  deleteBugStepAttachmentSocketEvent,
  deleteBugStepSocketEvent,
  bugStepAttachmentChangedSocketEvent,
  patchBugStepSocketEvent,
  updateBugStepsOrderSocketEvent,
} from "../model-bug-step";
import {
  createCommentAttachmentSocketEvent,
  createCommentSocketEvent,
  deleteCommentAttachmentSocketEvent,
  deleteCommentSocketEvent,
  commentAttachmentChangedSocketEvent,
  updateCommentSocketEvent,
} from "../model-comment";
import {
  createLinkSocketEvent,
  updateLinkSocketEvent,
  deleteLinkSocketEvent,
} from "../model-report-link";
import { useSocketEvent } from "@/shared/lib";
import { SocketEvent } from "@/shared/model";

export const useReportSocketEvents = () => {
  const reportId = useUnit($reportIdStore);
  const socketEvents = useUnit({
    patchReportSocketEvent,
    addParticipantSocketEvent,
    createLinkSocketEvent,
    updateLinkSocketEvent,
    deleteLinkSocketEvent,
    patchBugSocketEvent,
    createBugSocketEvent,
    bugAttachmentCreatedSocketEvent,
    bugAttachmentDeletedSocketEvent,
    bugAttachmentChangedSocketEvent,
    createCommentAttachmentSocketEvent,
    deleteCommentAttachmentSocketEvent,
    commentAttachmentChangedSocketEvent,
    createCommentSocketEvent,
    deleteCommentSocketEvent,
    updateCommentSocketEvent,
    createBugStepSocketEvent,
    patchBugStepSocketEvent,
    updateBugStepsOrderSocketEvent,
    deleteBugStepSocketEvent,
    createBugStepAttachmentSocketEvent,
    deleteBugStepAttachmentSocketEvent,
    bugStepAttachmentChangedSocketEvent,
  });

  useSocketEvent(SocketEvent.ReportPatch, (patch) =>
    socketEvents.patchReportSocketEvent(patch)
  );

  useSocketEvent(SocketEvent.ReportParticipant, (participantId) => {
    socketEvents.addParticipantSocketEvent(participantId);
  });

  useSocketEvent(SocketEvent.ReportLinkCreate, (link) => {
    socketEvents.createLinkSocketEvent(link);
  });

  useSocketEvent(SocketEvent.ReportLinkUpdate, (link) => {
    socketEvents.updateLinkSocketEvent(link);
  });

  useSocketEvent(SocketEvent.ReportLinkDelete, (linkId) => {
    socketEvents.deleteLinkSocketEvent(linkId);
  });

  useSocketEvent(SocketEvent.BugPatch, (patch) => {
    socketEvents.patchBugSocketEvent({
      bugId: patch.bugId,
      patch: patch.patch,
    });
  });

  useSocketEvent(SocketEvent.BugCreate, (bug) => {
    if (!reportId) return;

    socketEvents.createBugSocketEvent({
      reportId,
      bug: {
        ...bug,
        status: bug.status,
      },
    });
  });

  useSocketEvent(SocketEvent.BugAttachmentCreate, (attachment) => {
    if (isCommentAttachment(attachment.attachType)) return;

    socketEvents.bugAttachmentCreatedSocketEvent(attachment);
  });

  useSocketEvent(SocketEvent.BugAttachmentDelete, ({ id, entityId }) => {
    socketEvents.bugAttachmentDeletedSocketEvent({
      bugId: entityId,
      attachmentId: id,
    });
  });

  useSocketEvent(SocketEvent.BugAttachmentChanged, (attachment) => {
    if (isCommentAttachment(attachment.attachType)) return;

    socketEvents.bugAttachmentChangedSocketEvent(attachment);
  });

  useSocketEvent(SocketEvent.CommentAttachmentCreate, (attachment) => {
    if (!isCommentAttachment(attachment.attachType)) return;

    socketEvents.createCommentAttachmentSocketEvent(attachment);
  });

  useSocketEvent(SocketEvent.CommentAttachmentDelete, ({ id, entityId }) => {
    socketEvents.deleteCommentAttachmentSocketEvent({
      attachmentId: id,
      commentId: entityId,
    });
  });

  useSocketEvent(SocketEvent.CommentAttachmentChanged, (attachment) => {
    if (!isCommentAttachment(attachment.attachType)) return;

    socketEvents.commentAttachmentChangedSocketEvent(attachment);
  });

  useSocketEvent(SocketEvent.CommentCreate, (comment) => {
    socketEvents.createCommentSocketEvent(comment);
  });

  useSocketEvent(SocketEvent.CommentDelete, ({ bugId, commentId }) => {
    socketEvents.deleteCommentSocketEvent({ bugId, commentId });
  });

  useSocketEvent(SocketEvent.CommentUpdate, (comment) => {
    socketEvents.updateCommentSocketEvent(comment);
  });

  useSocketEvent(SocketEvent.BugStepCreate, (step) => {
    socketEvents.createBugStepSocketEvent(step);
  });

  useSocketEvent(SocketEvent.BugStepPatch, ({ bugId, step }) => {
    socketEvents.patchBugStepSocketEvent({ bugId, step });
  });

  useSocketEvent(SocketEvent.BugStepsOrderUpdate, ({ bugId, steps }) => {
    socketEvents.updateBugStepsOrderSocketEvent({ bugId, steps });
  });

  useSocketEvent(SocketEvent.BugStepDelete, ({ bugId, stepId }) => {
    socketEvents.deleteBugStepSocketEvent({ bugId, stepId });
  });

  useSocketEvent(SocketEvent.BugStepAttachmentCreate, (attachment) => {
    if (!isBugStepAttachment(attachment.attachType)) return;

    socketEvents.createBugStepAttachmentSocketEvent(attachment);
  });

  useSocketEvent(SocketEvent.BugStepAttachmentDelete, ({ id, entityId }) => {
    socketEvents.deleteBugStepAttachmentSocketEvent({
      stepId: entityId,
      attachmentId: id,
    });
  });

  useSocketEvent(SocketEvent.BugStepAttachmentChanged, (attachment) => {
    if (!isBugStepAttachment(attachment.attachType)) return;

    socketEvents.bugStepAttachmentChangedSocketEvent(attachment);
  });
};

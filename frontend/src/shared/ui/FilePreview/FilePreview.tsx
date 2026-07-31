import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Check, Copy, FileText, Film, Loader2, Trash2, X } from "lucide-react";

import { AttachmentChip, AttachFileButton } from "@/shared/ui";
import { reportsApi } from "@/shared/api";
import {
  copyToClipboard,
  createCurlAttachmentFile,
  getClipboardFiles,
} from "@/shared/lib";
import { AttachmentTypes } from "@/shared/config";
import { PendingAttachment } from "@/shared/ui";

// Адрес содержимого вложения — операция-адрес модуля `reports`, а не строка рядом.
const { attachmentContentUrl } = reportsApi;

type Attachment = {
  id: number;
  entityId: number;
  attachType: AttachmentTypes;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

type Props = {
  attachments: Attachment[];
  reportId: string;
  bugId: number;
  attachType: number;
  commentId?: number;
  stepId?: number;
  onAttachmentUpload?: (file: File) => void | Promise<unknown>;
  onAttachmentDelete?: (attachmentId: number) => void;
  onAttachmentRename?: (
    attachmentId: number,
    fileName: string
  ) => void | Promise<unknown>;
  currentUserId?: string;
  disabled?: boolean;
};

type UploadingAttachment = {
  id: number;
  file: File;
  objectUrl: string | null;
};

const imageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
const videoExtensions = [".mp4", ".webm", ".mov", ".ogg", ".mkv"];
const minZoomLevel = 1;
const maxZoomLevel = 4;
const zoomStep = 0.25;

const isImage = (fileName: string): boolean => {
  const extension = fileName.toLowerCase().substring(fileName.lastIndexOf("."));
  return imageExtensions.includes(extension);
};

const isVideo = (fileName: string): boolean => {
  const extension = fileName.toLowerCase().substring(fileName.lastIndexOf("."));
  return videoExtensions.includes(extension);
};

const isCurlAttachment = (fileName: string): boolean => {
  const normalizedFileName = fileName.toLowerCase();
  return (
    normalizedFileName.includes("curl") ||
    normalizedFileName === "curl-command.txt"
  );
};

function FilePreview({
  attachments,
  reportId,
  bugId,
  commentId,
  stepId,
  onAttachmentUpload,
  onAttachmentDelete,
  onAttachmentRename,
  currentUserId,
  disabled = false,
}: Props) {
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const [zoomLevel, setZoomLevel] = useState(minZoomLevel);
  const [imagePosition, setImagePosition] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [isVideoPlaying, setIsVideoPlaying] = useState(false);
  const [showOverlayControls, setShowOverlayControls] = useState(true);
  const [curlPreviewText, setCurlPreviewText] = useState("");
  const [isCurlPreviewLoading, setIsCurlPreviewLoading] = useState(false);
  const [curlPreviewError, setCurlPreviewError] = useState<string | null>(null);
  const [isCurlCopied, setIsCurlCopied] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const nextPendingAttachmentIdRef = useRef(0);
  const uploadingAttachmentsRef = useRef<UploadingAttachment[]>([]);
  const dragStartRef = useRef({ x: 0, y: 0 });
  const controlsHideTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(
    null
  );

  const activeAttachment =
    activeIndex !== null ? attachments[activeIndex] : undefined;
  const isActiveVideo = activeAttachment
    ? isVideo(activeAttachment.fileName)
    : false;
  const isActiveCurl = activeAttachment
    ? isCurlAttachment(activeAttachment.fileName)
    : false;
  const shouldHideOverlayControls =
    isActiveVideo && isVideoPlaying && !showOverlayControls;
  const previewCursor = isDragging
    ? "grabbing"
    : zoomLevel > minZoomLevel
      ? "zoom-out"
      : "zoom-in";
  const [uploadingAttachments, setUploadingAttachments] = useState<
    UploadingAttachment[]
  >([]);
  const [pendingCurlAttachments, setPendingCurlAttachments] = useState<
    PendingAttachment[]
  >([]);

  const resetImageTransform = () => {
    setZoomLevel(minZoomLevel);
    setImagePosition({ x: 0, y: 0 });
    setIsDragging(false);
  };

  const clampZoom = (value: number) => {
    return Math.min(Math.max(value, minZoomLevel), maxZoomLevel);
  };

  const clearControlsHideTimeout = () => {
    if (controlsHideTimeoutRef.current) {
      clearTimeout(controlsHideTimeoutRef.current);
      controlsHideTimeoutRef.current = null;
    }
  };

  const scheduleControlsHide = () => {
    clearControlsHideTimeout();
    controlsHideTimeoutRef.current = setTimeout(() => {
      setShowOverlayControls(false);
    }, 2000);
  };

  const resetVideoControlsState = () => {
    clearControlsHideTimeout();
    setIsVideoPlaying(false);
    setShowOverlayControls(true);
  };

  const handleImageClick = (index: number) => {
    setActiveIndex(index);
    resetImageTransform();
    resetVideoControlsState();
  };

  const handleCloseModal = () => {
    setActiveIndex(null);
    resetImageTransform();
    resetVideoControlsState();
  };

  const handlePrev = () => {
    if (activeIndex !== null && activeIndex > 0) {
      setActiveIndex(activeIndex - 1);
      resetImageTransform();
      resetVideoControlsState();
    }
  };

  const handleNext = () => {
    if (activeIndex !== null && activeIndex < attachments.length - 1) {
      setActiveIndex(activeIndex + 1);
      resetImageTransform();
      resetVideoControlsState();
    }
  };

  const handleImageWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    const zoomDelta = event.deltaY < 0 ? zoomStep : -zoomStep;
    setZoomLevel((previousZoom) => {
      const newZoom = clampZoom(previousZoom + zoomDelta);
      if (newZoom === minZoomLevel) {
        setImagePosition({ x: 0, y: 0 });
      }
      return newZoom;
    });
  };

  const handleImageMouseDown = (event: React.MouseEvent<HTMLDivElement>) => {
    if (zoomLevel <= minZoomLevel) {
      return;
    }

    setIsDragging(true);
    dragStartRef.current = {
      x: event.clientX - imagePosition.x,
      y: event.clientY - imagePosition.y,
    };
  };

  const handleImageMouseMove = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!isDragging || zoomLevel <= minZoomLevel) {
      return;
    }

    setImagePosition({
      x: event.clientX - dragStartRef.current.x,
      y: event.clientY - dragStartRef.current.y,
    });
  };

  const handleImageMouseUp = () => {
    if (!isDragging) {
      return;
    }
    setIsDragging(false);
  };

  const handleImageDoubleClick = () => {
    if (zoomLevel === minZoomLevel) {
      setZoomLevel(2);
      return;
    }
    resetImageTransform();
  };

  const handleVideoPlay = () => {
    setIsVideoPlaying(true);
    setShowOverlayControls(true);
    scheduleControlsHide();
  };

  const handleVideoPauseOrEnd = () => {
    resetVideoControlsState();
  };

  const handleViewerMouseMove = () => {
    if (!isActiveVideo || !isVideoPlaying) {
      return;
    }
    setShowOverlayControls(true);
    scheduleControlsHide();
  };

  const handleOverlayClick = (event: React.MouseEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget) {
      handleCloseModal();
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = event.target.files;
    if (!files || !onAttachmentUpload) return;

    Array.from(files).forEach((file) => {
      uploadAttachment(file);
    });
    // Сбрасываем значение input для возможности загрузки того же файла
    event.target.value = "";
  };

  const uploadAttachment = (file: File) => {
    if (!onAttachmentUpload) return;

    const pendingId = nextPendingAttachmentIdRef.current++;
    const objectUrl = isImage(file.name) ? URL.createObjectURL(file) : null;
    setUploadingAttachments((prev) => [
      ...prev,
      { id: pendingId, file, objectUrl },
    ]);

    try {
      Promise.resolve(onAttachmentUpload(file))
        .catch(() => undefined)
        .finally(() => {
          setUploadingAttachments((prev) => {
            const pending = prev.find((item) => item.id === pendingId);
            if (pending?.objectUrl) {
              URL.revokeObjectURL(pending.objectUrl);
            }
            return prev.filter((item) => item.id !== pendingId);
          });
        });
    } catch {
      setUploadingAttachments((prev) => {
        const pending = prev.find((item) => item.id === pendingId);
        if (pending?.objectUrl) {
          URL.revokeObjectURL(pending.objectUrl);
        }
        return prev.filter((item) => item.id !== pendingId);
      });
    }
  };

  const addPendingCurlAttachment = (file: File) => {
    const pendingCurlAttachment: PendingAttachment = {
      id: nextPendingAttachmentIdRef.current++,
      file,
      name: file.name,
      kind: "curl",
    };

    setPendingCurlAttachments((prev) => [...prev, pendingCurlAttachment]);
  };

  const renamePendingCurlAttachment = (id: number, name: string) => {
    setPendingCurlAttachments((prev) =>
      prev.map((attachment) =>
        attachment.id === id ? { ...attachment, name } : attachment
      )
    );
  };

  const removePendingCurlAttachment = (id: number) => {
    setPendingCurlAttachments((prev) =>
      prev.filter((attachment) => attachment.id !== id)
    );
  };

  const uploadPendingCurlAttachment = (
    attachment: PendingAttachment,
    name: string
  ) => {
    const file =
      name === attachment.file.name
        ? attachment.file
        : new File([attachment.file], name, {
            type: attachment.file.type,
            lastModified: attachment.file.lastModified,
          });

    removePendingCurlAttachment(attachment.id);
    uploadAttachment(file);
  };

  const handlePaste = (event: React.ClipboardEvent<HTMLDivElement>) => {
    if (disabled || !onAttachmentUpload) return;

    const files = getClipboardFiles(event.clipboardData);
    const clipboardText = event.clipboardData?.getData("text/plain") ?? "";
    const curlFile = createCurlAttachmentFile(clipboardText);
    if (!curlFile && files.length === 0) return;

    event.preventDefault();
    files.forEach((file) => uploadAttachment(file));
    if (curlFile) {
      addPendingCurlAttachment(curlFile);
    }
  };

  const handleUploadClick = () => {
    fileInputRef.current?.click();
  };

  const handleDeleteAttachment = (
    event: React.MouseEvent,
    attachmentId: number
  ) => {
    event.stopPropagation(); // Предотвращаем открытие карусели
    confirmDeleteAttachment(attachmentId);
  };

  const confirmDeleteAttachment = (attachmentId: number) => {
    const confirmed = window.confirm(
      "Вы уверены, что хотите удалить этот файл?"
    );

    if (!confirmed) {
      return;
    }

    if (onAttachmentDelete) {
      onAttachmentDelete(attachmentId);
    }
  };

  const deleteAttachment = (attachmentId: number) => {
    if (onAttachmentDelete) {
      onAttachmentDelete(attachmentId);
    }
  };

  // Адрес собирается из шаблона контракта (`shared/api/reports`), а не строкой
  // рядом: браузерный `src` обязан вести туда же, куда ходит axios.
  const getImageUrl = useCallback(
    ({ id }: Attachment, preview?: boolean) =>
      attachmentContentUrl({ reportId, bugId, id, commentId, stepId, preview }),
    [bugId, commentId, reportId, stepId]
  );

  const handleCopyCurl = async () => {
    if (!curlPreviewText) return;

    try {
      await copyToClipboard(curlPreviewText);
      setIsCurlCopied(true);
      window.setTimeout(() => setIsCurlCopied(false), 1500);
    } catch {
      setCurlPreviewError("Не удалось скопировать curl");
    }
  };

  const copyCurlAttachment = async (attachment: Attachment) => {
    const response = await fetch(getImageUrl(attachment), {
      credentials: "include",
    });

    if (!response.ok) {
      throw new Error("Failed to load curl attachment");
    }

    const text = await response.text();
    await copyToClipboard(text);
  };

  const copyPendingCurlAttachment = async (attachment: PendingAttachment) => {
    const text = await attachment.file.text();
    await copyToClipboard(text);
  };

  useEffect(() => {
    if (activeIndex === null) {
      return;
    }

    const clearControlsTimer = () => {
      if (controlsHideTimeoutRef.current) {
        clearTimeout(controlsHideTimeoutRef.current);
        controlsHideTimeoutRef.current = null;
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      const resetTransform = () => {
        setZoomLevel(minZoomLevel);
        setImagePosition({ x: 0, y: 0 });
        setIsDragging(false);
        clearControlsTimer();
        setIsVideoPlaying(false);
        setShowOverlayControls(true);
      };

      if (event.key === "Escape") {
        setActiveIndex(null);
        resetTransform();
      }

      if (event.key === "ArrowLeft") {
        event.preventDefault();
        if (activeIndex > 0) {
          setActiveIndex(activeIndex - 1);
          resetTransform();
        }
      }

      if (event.key === "ArrowRight") {
        event.preventDefault();
        if (activeIndex < attachments.length - 1) {
          setActiveIndex(activeIndex + 1);
          resetTransform();
        }
      }

      if (event.key === "+" || event.key === "=") {
        event.preventDefault();
        setZoomLevel((previousZoom) =>
          Math.min(
            Math.max(previousZoom + zoomStep, minZoomLevel),
            maxZoomLevel
          )
        );
      }

      if (event.key === "-" || event.key === "_") {
        event.preventDefault();
        setZoomLevel((previousZoom) => {
          const newZoom = Math.min(
            Math.max(previousZoom - zoomStep, minZoomLevel),
            maxZoomLevel
          );
          if (newZoom === minZoomLevel) {
            setImagePosition({ x: 0, y: 0 });
          }
          return newZoom;
        });
      }

      if (event.key === "0") {
        event.preventDefault();
        resetTransform();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      clearControlsTimer();
    };
  }, [activeIndex, attachments.length]);

  useEffect(() => {
    if (!isActiveVideo) {
      if (controlsHideTimeoutRef.current) {
        clearTimeout(controlsHideTimeoutRef.current);
        controlsHideTimeoutRef.current = null;
      }
      setIsVideoPlaying(false);
      setShowOverlayControls(true);
    }
    return () => {
      if (controlsHideTimeoutRef.current) {
        clearTimeout(controlsHideTimeoutRef.current);
        controlsHideTimeoutRef.current = null;
      }
    };
  }, [isActiveVideo]);

  useEffect(() => {
    if (!activeAttachment || !isActiveCurl) {
      setCurlPreviewText("");
      setCurlPreviewError(null);
      setIsCurlPreviewLoading(false);
      setIsCurlCopied(false);
      return;
    }

    const abortController = new AbortController();
    setCurlPreviewText("");
    setCurlPreviewError(null);
    setIsCurlPreviewLoading(true);
    setIsCurlCopied(false);

    fetch(getImageUrl(activeAttachment), {
      credentials: "include",
      signal: abortController.signal,
    })
      .then((response) => {
        if (!response.ok) {
          throw new Error("Failed to load curl attachment");
        }

        return response.text();
      })
      .then((text) => setCurlPreviewText(text))
      .catch((error) => {
        if (error instanceof DOMException && error.name === "AbortError") {
          return;
        }

        setCurlPreviewError("Не удалось загрузить содержимое curl");
      })
      .finally(() => {
        if (!abortController.signal.aborted) {
          setIsCurlPreviewLoading(false);
        }
      });

    return () => abortController.abort();
  }, [activeAttachment, getImageUrl, isActiveCurl]);

  useEffect(() => {
    uploadingAttachmentsRef.current = uploadingAttachments;
  }, [uploadingAttachments]);

  useEffect(() => {
    return () => {
      uploadingAttachmentsRef.current.forEach((attachment) => {
        if (attachment.objectUrl) {
          URL.revokeObjectURL(attachment.objectUrl);
        }
      });
    };
  }, []);

  return (
    <>
      {/* Скрытый input для выбора файла */}
      <input
        ref={fileInputRef}
        type="file"
        multiple
        style={{ display: "none" }}
        onChange={handleFileChange}
      />

      <div
        className="flex gap-2 mt-2 focus:outline-none"
        onPaste={handlePaste}
        tabIndex={!disabled && onAttachmentUpload ? 0 : -1}
        aria-label="Область вложений"
      >
        {attachments.map((attachment, index) => {
          const isImageFile = isImage(attachment.fileName);
          const isVideoFile = isVideo(attachment.fileName);
          const isCurlFile = isCurlAttachment(attachment.fileName);
          const shouldShowPreview =
            attachment.hasPreview && (isImageFile || isVideoFile);
          const canDeleteAttachment =
            !disabled &&
            !!onAttachmentDelete &&
            currentUserId === attachment.creatorUserId;
          const canRenameAttachment =
            !disabled &&
            !!onAttachmentRename &&
            currentUserId === attachment.creatorUserId;

          if (isCurlFile) {
            return (
              <AttachmentChip
                key={attachment.id}
                name={attachment.fileName}
                kind="curl"
                canRename={canRenameAttachment}
                canRemove={canDeleteAttachment}
                disabled={disabled}
                onRename={(name) => onAttachmentRename?.(attachment.id, name)}
                onOpen={() => handleImageClick(index)}
                onRemove={() => deleteAttachment(attachment.id)}
                onCopy={() => copyCurlAttachment(attachment)}
              />
            );
          }

          return (
            <div key={attachment.id} className="relative group">
              <button
                className="btn btn-ghost btn-md p-0 btn-square overflow-hidden border-base-300"
                onClick={() => handleImageClick(index)}
              >
                {shouldShowPreview ? (
                  <img
                    src={getImageUrl(attachment, true)}
                    alt={attachment.fileName}
                    className="w-full h-full object-cover"
                  />
                ) : isImageFile ? (
                  <div className="relative flex h-full w-full items-center justify-center bg-base-200">
                    <FileText className="h-4 w-4 text-info opacity-60" />
                    <Loader2 className="absolute h-4 w-4 animate-spin text-primary" />
                  </div>
                ) : (
                  <>
                    {isVideoFile ? (
                      <Film className="w-4 h-4 text-info" />
                    ) : (
                      <FileText className="w-4 h-4 text-info" />
                    )}
                  </>
                )}
              </button>

              {canDeleteAttachment && (
                <button
                  className="absolute -top-1 -right-1 bg-error text-error-content rounded-full p-0.5 opacity-0 group-hover:opacity-100 transition-opacity duration-200 hover:bg-error-focus cursor-pointer"
                  onClick={(event) =>
                    handleDeleteAttachment(event, attachment.id)
                  }
                  title="Удалить файл"
                >
                  <Trash2 className="w-3 h-3" />
                </button>
              )}
            </div>
          );
        })}
        {uploadingAttachments.map((attachment) => {
          const isImageFile = isImage(attachment.file.name);

          return (
            <div
              key={attachment.id}
              className="relative flex h-12 w-12 items-center justify-center overflow-hidden rounded-btn border border-base-300 bg-base-200"
              title={attachment.file.name}
            >
              {isImageFile && attachment.objectUrl ? (
                <img
                  src={attachment.objectUrl}
                  alt={attachment.file.name}
                  className="h-full w-full object-cover opacity-70"
                />
              ) : (
                <FileText className="h-4 w-4 text-info opacity-70" />
              )}
              <div className="absolute inset-0 flex items-center justify-center bg-base-100/55 backdrop-blur-[1px]">
                <Loader2 className="h-4 w-4 animate-spin text-primary" />
              </div>
            </div>
          );
        })}
        {pendingCurlAttachments.map((attachment) => (
          <AttachmentChip
            key={attachment.id}
            name={attachment.name}
            kind={attachment.kind}
            disabled={disabled}
            autoEdit
            onRename={(name) =>
              renamePendingCurlAttachment(attachment.id, name)
            }
            onRemove={() => removePendingCurlAttachment(attachment.id)}
            onCommit={(name) => uploadPendingCurlAttachment(attachment, name)}
            onCopy={() => copyPendingCurlAttachment(attachment)}
          />
        ))}
        {!disabled && <AttachFileButton onClick={handleUploadClick} />}
      </div>

      {/* Модалка с каруселью */}
      {activeAttachment &&
        activeIndex !== null &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            className="fixed inset-0 z-[1000] bg-base-100"
            onClick={handleOverlayClick}
          >
            <div
              className="relative h-[100dvh] w-full bg-base-100 p-[clamp(0.5rem,2vw,1rem)]"
              onMouseMove={handleViewerMouseMove}
            >
              <button
                className={`btn btn-circle btn-sm absolute right-3 top-3 z-10 border-0 bg-base-content/35 text-base-100 shadow-xl backdrop-blur-sm transition-opacity duration-500 ease-in-out hover:bg-base-content/40 ${
                  shouldHideOverlayControls
                    ? "pointer-events-none opacity-0"
                    : ""
                }`}
                onClick={handleCloseModal}
                aria-label="Закрыть"
                title="Закрыть"
              >
                <X className="h-4 w-4" />
              </button>

              <div className="mb-3 flex flex-wrap items-center justify-between gap-3 pr-12">
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold">
                    {activeAttachment.fileName}
                  </p>
                  <p className="text-xs text-base-content/60">
                    {activeIndex + 1} из {attachments.length}
                  </p>
                </div>
              </div>

              {isImage(activeAttachment.fileName) ? (
                <div
                  className="relative h-[88dvh] overflow-hidden rounded-box"
                  style={{ cursor: previewCursor }}
                  onWheel={handleImageWheel}
                  onMouseDown={handleImageMouseDown}
                  onMouseMove={handleImageMouseMove}
                  onMouseUp={handleImageMouseUp}
                  onMouseLeave={handleImageMouseUp}
                  onDoubleClick={handleImageDoubleClick}
                >
                  <img
                    src={getImageUrl(activeAttachment)}
                    alt={activeAttachment.fileName}
                    className="pointer-events-none absolute left-1/2 top-1/2 max-h-full max-w-full select-none"
                    style={{
                      transform: `translate(-50%, -50%) translate(${imagePosition.x}px, ${imagePosition.y}px) scale(${zoomLevel})`,
                      transformOrigin: "center center",
                    }}
                    draggable={false}
                  />
                </div>
              ) : isVideo(activeAttachment.fileName) ? (
                <video
                  className="h-[88dvh] w-full rounded-box bg-base-200"
                  controls
                  playsInline
                  src={getImageUrl(activeAttachment)}
                  onPlay={handleVideoPlay}
                  onPause={handleVideoPauseOrEnd}
                  onEnded={handleVideoPauseOrEnd}
                />
              ) : isActiveCurl ? (
                <div className="relative flex h-[88dvh] flex-col overflow-hidden rounded-box border border-base-300 bg-base-200">
                  <button
                    type="button"
                    className="btn btn-square btn-sm absolute right-3 top-3 z-10 cursor-pointer border-info/25 bg-info/10 text-info shadow-sm backdrop-blur transition-colors hover:border-info/35 hover:bg-info/15 hover:text-info"
                    onClick={handleCopyCurl}
                    disabled={
                      isCurlPreviewLoading ||
                      !!curlPreviewError ||
                      !curlPreviewText
                    }
                    aria-label={
                      isCurlCopied ? "Curl скопирован" : "Скопировать curl"
                    }
                    title={
                      isCurlCopied ? "Curl скопирован" : "Скопировать curl"
                    }
                  >
                    {isCurlCopied ? (
                      <Check className="h-4 w-4" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </button>
                  {isCurlPreviewLoading ? (
                    <div className="flex flex-1 items-center justify-center">
                      <Loader2 className="h-6 w-6 animate-spin text-primary" />
                    </div>
                  ) : curlPreviewError ? (
                    <div className="flex flex-1 items-center justify-center p-6 text-center">
                      <div>
                        <FileText className="mx-auto mb-4 h-12 w-12 text-base-content/40" />
                        <p className="text-sm font-medium text-base-content">
                          {curlPreviewError}
                        </p>
                        <a
                          className="btn btn-sm btn-info btn-soft mt-4"
                          href={getImageUrl(activeAttachment)}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Открыть файл
                        </a>
                      </div>
                    </div>
                  ) : (
                    <pre className="m-0 flex-1 overflow-auto whitespace-pre-wrap break-words p-4 pr-14 pt-12 font-mono text-xs leading-5 text-base-content">
                      {curlPreviewText}
                    </pre>
                  )}
                </div>
              ) : (
                <div className="flex h-64 items-center justify-center rounded-box bg-base-200">
                  <div className="text-center">
                    <FileText className="w-16 h-16 text-base-content/40 mx-auto mb-4" />
                    <p className="text-lg font-semibold">
                      {activeAttachment.fileName}
                    </p>
                    <p className="text-sm text-base-content/60">Файл</p>
                    <a
                      className="btn btn-sm btn-info btn-soft mt-4"
                      href={getImageUrl(activeAttachment)}
                      target="_blank"
                      rel="noreferrer"
                    >
                      Открыть файл
                    </a>
                  </div>
                </div>
              )}

              {/* Стрелки навигации */}
              {activeIndex > 0 && (
                <button
                  className={`btn btn-circle btn-lg absolute inset-y-0 left-2 z-10 my-auto border-0 bg-base-content/35 text-base-100 shadow-xl backdrop-blur-sm transition-opacity duration-500 ease-in-out hover:bg-base-content/40 ${
                    shouldHideOverlayControls
                      ? "pointer-events-none opacity-0"
                      : ""
                  }`}
                  onClick={handlePrev}
                  aria-label="Предыдущее вложение"
                >
                  ❮
                </button>
              )}

              {activeIndex < attachments.length - 1 && (
                <button
                  className={`btn btn-circle btn-lg absolute inset-y-0 right-2 z-10 my-auto border-0 bg-base-content/35 text-base-100 shadow-xl backdrop-blur-sm transition-opacity duration-500 ease-in-out hover:bg-base-content/40 ${
                    shouldHideOverlayControls
                      ? "pointer-events-none opacity-0"
                      : ""
                  }`}
                  onClick={handleNext}
                  aria-label="Следующее вложение"
                >
                  ❯
                </button>
              )}
            </div>
          </div>,
          document.body
        )}
    </>
  );
}

export default FilePreview;

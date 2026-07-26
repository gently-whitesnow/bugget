import { useEffect, useMemo, useRef, useState } from "react";
import { formatDistanceToNow } from "date-fns";
import { ru } from "date-fns/locale";
import { useUnit } from "effector-react";
import { useParams, useNavigate, useLocation } from "react-router";
import { debounce } from "throttle-debounce";
import { AutoResizeTextarea } from "@/shared/ui";

import { useReportPageSocket, useReportSocketEvents } from "../lib";
import { bugHashPattern, useScrollToHash } from "../lib";
import {
  $creatorUserNameStore,
  $initialReportStore,
  $titleStore,
  $bugStatusFilterStore,
  changeTitleEvent,
  clearReport,
  saveTitleEvent,
  updateReportPathIdEvent,
  clearBugsEvent,
} from "@/entities/report";
import {
  $externalSearchResultsStore,
  clearExternalSearchResultsEvent,
  searchExternalFx,
  selectExternalSearchItemEvent,
} from "../model";
import {
  $combinedBugsStore,
  $newBugStore,
  createNewBugEvent,
} from "@/pages/Report";
import type { ExternalSearchItem } from "../api/contracts";

import { Bug } from "./components/Bug";
import ExternalSearchResults from "./components/ExternalSearchResults/ExternalSearchResults";
import { ReportLinks } from "./components/ReportLinks";
import { FloatingAddBugButton } from "./components/FloatingAddBugButton";

const ReportPage = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { reportId } = useParams();

  const initialReport = useUnit($initialReportStore);
  const title = useUnit($titleStore);
  const creatorUserName = useUnit($creatorUserNameStore);
  const allBugs = useUnit($combinedBugsStore);
  const bugStatusFilter = useUnit($bugStatusFilterStore);
  const externalSearchResults = useUnit($externalSearchResultsStore);
  const newBug = useUnit($newBugStore);
  const reportActions = useUnit({
    searchExternalFx,
    clearExternalSearchResultsEvent,
    saveTitleEvent,
    updateReportPathIdEvent,
    clearReport,
    clearBugsEvent,
    createNewBugEvent,
    selectExternalSearchItemEvent,
    changeTitleEvent,
  });

  useReportPageSocket();
  useReportSocketEvents();

  const isNewReport = !reportId;

  const [isExternalSearchOpen, setIsExternalSearchOpen] = useState(isNewReport);
  const [isAddBugButtonVisible, setIsAddBugButtonVisible] = useState(true);
  const addBugButtonRef = useRef<HTMLButtonElement>(null);

  const debouncedExternalSearch = useMemo(
    () =>
      debounce(300, (value: string) => {
        reportActions.searchExternalFx(value);
      }),
    [reportActions]
  );

  const SaveTitleHandler = () => {
    if (!title.trim()) {
      return;
    }
    if (isNewReport) {
      setIsExternalSearchOpen(false);
      reportActions.clearExternalSearchResultsEvent();
    }

    reportActions.saveTitleEvent();
  };
  // состояние страницы: привязываем reportId к стору
  useEffect(() => {
    reportActions.updateReportPathIdEvent(reportId ?? null);
  }, [reportActions, reportId]);

  // очищаем состояние при смене reportId / размонтировании
  useEffect(() => {
    reportActions.clearReport();
    reportActions.clearBugsEvent();
    reportActions.clearExternalSearchResultsEvent();

    return () => {
      reportActions.clearReport();
      reportActions.clearBugsEvent();
      reportActions.clearExternalSearchResultsEvent();
    };
  }, [reportActions, reportId]);

  // редирект после создания репорта
  useEffect(() => {
    if (isNewReport && initialReport?.id) {
      navigate(`${location.pathname}/${initialReport.id}`);
    }
  }, [isNewReport, initialReport?.id, navigate, location.pathname]);

  // внешняя подсказка по заголовку
  useEffect(() => {
    // если это уже существующий репорт — ничего не ищем
    if (!isNewReport) {
      setIsExternalSearchOpen(false);
      reportActions.clearExternalSearchResultsEvent();
      return;
    }
    // если панель закрыта — тоже не ищем
    if (!isExternalSearchOpen) {
      reportActions.clearExternalSearchResultsEvent();
      return;
    }

    // вызываем поиск даже с пустой строкой,
    // чтобы бэкенд мог показать "дефолтные" результаты
    const query = title ?? "";
    debouncedExternalSearch(query);
  }, [
    title,
    isNewReport,
    isExternalSearchOpen,
    debouncedExternalSearch,
    reportActions,
  ]);

  // Автоматически создаем локальный баг, если репорт существует, но багов еще нет
  useEffect(() => {
    if (reportId && initialReport && allBugs.length === 0 && !newBug) {
      reportActions.createNewBugEvent({ reportId, bugCount: 0 });
    }
  }, [reportId, initialReport, allBugs.length, newBug, reportActions]);

  // скролл к багу по хэшу в URL (например, #bug-9)
  useScrollToHash({
    items: allBugs,
    getId: (bug) => bug.id,
    hashPattern: bugHashPattern,
    resetKey: reportId,
  });

  useEffect(() => {
    const button = addBugButtonRef.current;
    if (!button) return;

    const observer = new IntersectionObserver(
      ([entry]) => setIsAddBugButtonVisible(entry.isIntersecting),
      { threshold: 0 }
    );
    observer.observe(button);
    return () => observer.disconnect();
  }, [allBugs]);

  const handleAddBugClick = () => {
    if (!reportId) return;

    reportActions.createNewBugEvent({ reportId, bugCount: allBugs.length });
  };

  const handleTitleFocus = () => {
    if (isNewReport) {
      setIsExternalSearchOpen(true);
    }
  };

  const handleTitleBlur = () => {
    SaveTitleHandler();
  };

  const handleTitleCancel = () => {
    if (!isNewReport) return;

    setIsExternalSearchOpen(false);
    reportActions.clearExternalSearchResultsEvent();
  };

  const handleExternalResultSelect = (item: ExternalSearchItem) => {
    if (!isNewReport) return;

    reportActions.selectExternalSearchItemEvent(item);
    reportActions.changeTitleEvent(item.text);
    SaveTitleHandler();
  };

  return (
    <>
      <AutoResizeTextarea
        value={title}
        onChange={reportActions.changeTitleEvent}
        onBlur={handleTitleBlur}
        onSave={handleTitleBlur}
        onCancel={handleTitleCancel}
        onFocus={handleTitleFocus}
        autoFocus={isNewReport}
        placeholder="Заголовок репорта"
        maxLength={128}
        className="
          w-full
          bg-transparent border-none
          text-2xl font-semi
          leading-snug
          focus:outline-none focus:ring-0
          placeholder:text-base-content/40
          py-1
        "
      />

      {isNewReport && isExternalSearchOpen && (
        <ExternalSearchResults
          items={externalSearchResults}
          onSelect={handleExternalResultSelect}
        />
      )}

      {reportId && (
        <div>
          Создан{" "}
          {initialReport?.createdAt
            ? formatDistanceToNow(new Date(initialReport.createdAt), {
                addSuffix: true,
                locale: ru,
              })
            : ""}{" "}
          пользователем <strong>{creatorUserName || "Загрузка..."}</strong>
        </div>
      )}

      {reportId && <ReportLinks />}

      <div className="flex flex-col gap-2">
        {allBugs
          .filter(
            (bug) =>
              bugStatusFilter === null ||
              bug.isLocalOnly ||
              bug.status === bugStatusFilter
          )
          .map((bug) => (
            <Bug key={bug.clientId} bug={bug} totalBugsCount={allBugs.length} />
          ))}
        {!allBugs.some((bug) => bug.isLocalOnly) && (
          <button
            ref={addBugButtonRef}
            className="btn btn-outline btn-primary font-normal ml-auto"
            onClick={handleAddBugClick}
            disabled={!title.trim()}
          >
            Добавить баг
          </button>
        )}
      </div>

      {!allBugs.some((bug) => bug.isLocalOnly) && (
        <FloatingAddBugButton
          onClick={handleAddBugClick}
          disabled={!title.trim()}
          visible={!isAddBugButtonVisible}
        />
      )}
    </>
  );
};

export default ReportPage;

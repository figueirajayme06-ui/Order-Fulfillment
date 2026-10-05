import { useCallback, useEffect, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import {
  FrappeGantt,
  type FrappeGanttNavigation,
  type FrappeGanttNavigationState,
  type FrappeGanttVisibleRange,
} from "../../components/timeline/FrappeGantt";
import { TimelineNavigation } from "../../components/timeline/TimelineNavigation";
import { Button, Spinner } from "../../components/common";
import {
  fetchAgreementDetail,
  fetchReservationsForHeader,
  type AgreementDetail,
} from "../../services/agreementsService";
import type { Reservation } from "../../types";
import { buildAgreementTimelineTasks } from "./agreementTimelineModel";
import { formatAssetTimelineRange, toAssetTimelineDateValue } from "../assets/assetsTimelineModel";
import styles from "./TimelinePage.module.css";

export const TimelinePage: FC = () => {
  const { headerId } = useParams<{ headerId: string }>();
  const { t, i18n } = useTranslation();
  const [detail, setDetail] = useState<AgreementDetail | null>(null);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [visibleRange, setVisibleRange] = useState<FrappeGanttVisibleRange | null>(null);
  const ganttNavigationRef = useRef<FrappeGanttNavigation>(null);
  const [timelineNavigation, setTimelineNavigation] = useState<FrappeGanttNavigationState>({
    canPanEarlier: false,
    canPanLater: false,
  });

  const loadData = useCallback(async () => {
    if (!headerId) return;
    setIsLoading(true);
    setLoadError(false);
    try {
      const agreementData = await fetchAgreementDetail(Number(headerId));
      const reservationData = await fetchReservationsForHeader(Number(headerId));
      setDetail(agreementData);
      setReservations(reservationData);
    } catch {
      setLoadError(true);
    } finally {
      setIsLoading(false);
    }
  }, [headerId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const tasks = useMemo(
    () => (detail ? buildAgreementTimelineTasks(detail.lines, reservations, toAssetTimelineDateValue(new Date())) : []),
    [detail, reservations],
  );

  if (isLoading) {
    return (
      <div className={styles.loading}>
        <Spinner />
      </div>
    );
  }

  if (loadError || !detail) {
    return (
      <div className={styles.error} role="alert">
        <p>{t("timeline.loadError")}</p>
        <Button label={t("timeline.retry")} onClick={() => void loadData()} />
      </div>
    );
  }

  const { header, lines } = detail;

  return (
    <div className={styles.page}>
      <h1>{t("timeline.title", { agreement: header.agreementNumber ?? `#${header.id}` })}</h1>
      <p className={styles.subtitle}>
        {header.customerName} · {header.division} ·{" "}
        {t("timeline.counts", { lines: lines.length, reservations: reservations.length })}
      </p>
      <div className={styles.legend} aria-label={t("timeline.legend")}>
        <span>{t("timeline.line")}</span>
        <span className="bar-reservation">{t("timeline.reservation")}</span>
        <span className="bar-depot">{t("timeline.depot")}</span>
        <span className="bar-rehire">{t("timeline.rehire")}</span>
      </div>
      <div className={styles.ganttContainer} data-print-timeline>
        <div className={styles.timelineToolbar}>
          <span>{visibleRange && formatAssetTimelineRange(visibleRange.start, visibleRange.end)}</span>
          <span data-print-hidden>
            <TimelineNavigation
              canPanEarlier={timelineNavigation.canPanEarlier}
              canPanLater={timelineNavigation.canPanLater}
              onPanEarlier={() => ganttNavigationRef.current?.panEarlier()}
              onPanLater={() => ganttNavigationRef.current?.panLater()}
            />
          </span>
        </div>
        <FrappeGantt
          ref={ganttNavigationRef}
          tasks={tasks}
          viewMode="Week"
          language={i18n.language.split("-")[0]}
          readonlyDates
          readonlyProgress
          onVisibleRangeChange={setVisibleRange}
          onNavigationStateChange={setTimelineNavigation}
        />
      </div>
    </div>
  );
};

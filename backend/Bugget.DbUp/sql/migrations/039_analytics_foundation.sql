-- Analytics foundation:
-- 1) `is_excluded_from_analytics` — per-report opt-out flag.
-- 2) `report_phase_intervals` — read-model of phase intervals built from
--    domain events. Idempotency via UNIQUE(source_event_id); at-most-one
--    active interval per report via partial unique index on (report_id)
--    WHERE exited_at IS NULL.

ALTER TABLE public.reports
    ADD COLUMN IF NOT EXISTS is_excluded_from_analytics boolean NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS idx_reports_excluded
    ON public.reports (is_excluded_from_analytics)
    WHERE is_excluded_from_analytics = TRUE;

CREATE TABLE IF NOT EXISTS public.report_phase_intervals (
    id                     bigserial   PRIMARY KEY,
    report_id              integer     NOT NULL REFERENCES public.reports(id) ON DELETE CASCADE,
    phase                  smallint    NOT NULL,
    entered_at             timestamptz NOT NULL,
    exited_at              timestamptz NULL,
    duration_seconds       bigint      GENERATED ALWAYS AS
                               (EXTRACT(EPOCH FROM (exited_at - entered_at))::bigint)
                               STORED,
    regression_cycle_index integer     NOT NULL DEFAULT 0,
    source_event_id        bigint      NOT NULL,
    UNIQUE (source_event_id)
);

CREATE INDEX IF NOT EXISTS idx_rpi_report_phase
    ON public.report_phase_intervals (report_id, phase);

CREATE INDEX IF NOT EXISTS idx_rpi_exited_at
    ON public.report_phase_intervals (exited_at)
    WHERE exited_at IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_rpi_active
    ON public.report_phase_intervals (report_id)
    WHERE exited_at IS NULL;

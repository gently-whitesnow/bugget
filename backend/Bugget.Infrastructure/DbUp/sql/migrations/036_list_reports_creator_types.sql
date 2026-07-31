-- Add _creator_types int[] filter to list_reports_base / list_reports_ids / list_reports_count.
-- Used by GET /v2/reports?creatorTypes=2 to inline tester-authored reports under team page #beta-test
-- and on /workspaces/:id/beta-test page (PRD §7.2). Column reports.creator_type added in migration 030.

DROP FUNCTION IF EXISTS public.list_reports_base(text, text, text, int[]);
CREATE OR REPLACE FUNCTION public.list_reports_base(
    _organization_id   text DEFAULT NULL,
    _user_id           text DEFAULT NULL,
    _team_id           text DEFAULT NULL,
    _report_statuses   int[] DEFAULT NULL,
    _creator_types     int[] DEFAULT NULL
)
RETURNS TABLE(
    id          int,
    created_at  timestamptz,
    updated_at  timestamptz
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.id,
        r.created_at,
        r.updated_at
    FROM public.reports r
    WHERE (_organization_id IS NULL OR r.creator_organization_id = _organization_id)
      AND (_team_id        IS NULL OR r.creator_team_id        = _team_id)
      AND (_report_statuses IS NULL OR r.status = ANY(_report_statuses))
      AND (_creator_types   IS NULL OR r.creator_type = ANY(_creator_types))
      AND (_user_id        IS NULL OR EXISTS (
            SELECT 1
            FROM public.report_participants rp
            WHERE rp.report_id = r.id
              AND rp.user_id   = _user_id
      ));
$$;

DROP FUNCTION IF EXISTS public.list_reports_ids(int, int, text, text, text, int[]);
CREATE OR REPLACE FUNCTION public.list_reports_ids(
    _skip              int,
    _take              int,
    _organization_id   text DEFAULT NULL,
    _user_id           text DEFAULT NULL,
    _team_id           text DEFAULT NULL,
    _report_statuses   int[] DEFAULT NULL,
    _creator_types     int[] DEFAULT NULL
)
RETURNS TABLE(id int)
LANGUAGE sql
STABLE
AS $$
    SELECT
        b.id
    FROM public.list_reports_base(_organization_id, _user_id, _team_id, _report_statuses, _creator_types) AS b
    ORDER BY
        b.updated_at DESC NULLS FIRST,
        b.created_at DESC
    OFFSET GREATEST(_skip, 0)
    LIMIT  GREATEST(_take, 0);
$$;

DROP FUNCTION IF EXISTS public.list_reports_count(text, text, text, int[]);
CREATE OR REPLACE FUNCTION public.list_reports_count(
    _organization_id   text DEFAULT NULL,
    _user_id           text DEFAULT NULL,
    _team_id           text DEFAULT NULL,
    _report_statuses   int[] DEFAULT NULL,
    _creator_types     int[] DEFAULT NULL
)
RETURNS bigint
LANGUAGE sql
STABLE
AS $$
    SELECT COUNT(*)
    FROM public.list_reports_base(_organization_id, _user_id, _team_id, _report_statuses, _creator_types);
$$;

-- Team-local report id support + resolve updates

ALTER TABLE public.reports
    ADD COLUMN IF NOT EXISTS team_report_id integer;

CREATE TABLE IF NOT EXISTS public.report_team_counters (
    team_id text PRIMARY KEY,
    last_report_id integer NOT NULL
);

-- Backfill team_report_id for existing reports (by creator_team_id)
WITH ranked AS (
    SELECT
        r.id,
        ROW_NUMBER() OVER (PARTITION BY r.creator_team_id ORDER BY r.id) AS rn
    FROM public.reports r
    WHERE r.creator_team_id IS NOT NULL
      AND r.team_report_id IS NULL
)
UPDATE public.reports r
SET team_report_id = ranked.rn
FROM ranked
WHERE r.id = ranked.id;

-- Initialize counters for existing teams
INSERT INTO public.report_team_counters(team_id, last_report_id)
SELECT r.creator_team_id, MAX(r.team_report_id)
FROM public.reports r
WHERE r.creator_team_id IS NOT NULL
GROUP BY r.creator_team_id
ON CONFLICT (team_id) DO UPDATE
SET last_report_id = EXCLUDED.last_report_id;

CREATE UNIQUE INDEX IF NOT EXISTS ux_reports_team_report_id
    ON public.reports (creator_team_id, team_report_id);

CREATE INDEX IF NOT EXISTS ix_reports_team_report_id
    ON public.reports (team_report_id);

-- обновляем resolve_report_id (расширяем сигнатуру)
DROP FUNCTION IF EXISTS public.resolve_report_id(text, integer, uuid);
CREATE OR REPLACE FUNCTION public.resolve_report_id(
    _workspace_id text DEFAULT NULL,
    _id integer DEFAULT NULL,
    _public_id uuid DEFAULT NULL,
    _team_report_id integer DEFAULT NULL
)
RETURNS TABLE(
    id integer,
    creator_team_id text,
    team_report_id integer
)
LANGUAGE plpgsql
STABLE
AS $$
BEGIN
    IF _id IS NOT NULL THEN
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.id = _id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id);
        RETURN;
    ELSIF _team_report_id IS NOT NULL THEN
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.team_report_id = _team_report_id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id);
        IF FOUND THEN
            RETURN;
        END IF;

        -- fallback: treat team_report_id as legacy global id
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.id = _team_report_id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id);
        RETURN;
    ELSIF _public_id IS NOT NULL THEN
        RETURN QUERY
        SELECT
            r.id,
            r.creator_team_id,
            r.team_report_id
        FROM public.reports r
        WHERE r.public_id = _public_id
          AND (_workspace_id IS NULL OR r.creator_organization_id = _workspace_id);
        RETURN;
    END IF;
END;
$$;

-- обновляем create_report_v3 / get_report_internal / patch_report_internal / list_reports_internal
DROP FUNCTION IF EXISTS public.create_report_v3(text, text, text, text);
CREATE OR REPLACE FUNCTION public.create_report_v3(_user_id text, _title text, _team_id text DEFAULT NULL, _organization_id text DEFAULT NULL)
    RETURNS TABLE(
        id int,
        team_report_id int,
        public_id uuid,
        title text,
        status int,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text)
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_report_id integer;
    new_team_report_id integer;
BEGIN
    IF _team_id IS NOT NULL THEN
        INSERT INTO public.report_team_counters(team_id, last_report_id)
            VALUES (_team_id, 1)
        ON CONFLICT (team_id) DO UPDATE
            SET last_report_id = public.report_team_counters.last_report_id + 1
        RETURNING last_report_id INTO new_team_report_id;
    END IF;

    -- Создаём Report
    INSERT INTO public.reports(responsible_user_id, title, status, creator_user_id, creator_team_id, creator_organization_id, past_responsible_user_id, team_report_id)
        VALUES (_user_id, _title, 0, _user_id, _team_id, _organization_id, _user_id, new_team_report_id)
    RETURNING
        public.reports.id INTO new_report_id;
    -- Добавляем участников
    INSERT INTO public.report_participants(report_id, user_id)
        VALUES (new_report_id, _user_id);
    RETURN QUERY
    SELECT
        r.id,
        r.team_report_id,
        r.public_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id
    FROM
        public.reports AS r
    WHERE
        r.id = new_report_id;
END;
$$;

DROP FUNCTION IF EXISTS public.get_report_internal(int);
CREATE OR REPLACE FUNCTION public.get_report_internal(_report_id int)
    RETURNS TABLE(
        id int,
        team_report_id int,
        public_id uuid,
        title text,
        status int,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text)
    LANGUAGE plpgsql
    STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.id,
        r.team_report_id,
        r.public_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id
    FROM
        public.reports r
    WHERE
        r.id = _report_id;
END;
$$;

DROP FUNCTION IF EXISTS public.patch_report_internal(int, text, integer, text);
CREATE OR REPLACE FUNCTION public.patch_report_internal(_report_id int, _title text DEFAULT NULL, _status integer DEFAULT NULL, _responsible_user_id text DEFAULT NULL)
    RETURNS TABLE(
        id integer,
        team_report_id int,
        public_id uuid,
        title text,
        status integer,
        responsible_user_id text,
        past_responsible_user_id text,
        updated_at timestamp with time zone,
        creator_team_id text)
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE
        public.reports AS r
    SET
        updated_at = now(),
        past_responsible_user_id = CASE WHEN _responsible_user_id IS NOT NULL THEN
            r.responsible_user_id
        ELSE
            r.past_responsible_user_id
        END,
        status = COALESCE(_status, r.status),
        title = COALESCE(_title, r.title),
        responsible_user_id = COALESCE(_responsible_user_id, r.responsible_user_id)
    WHERE
        r.id = _report_id;
    RETURN QUERY
    SELECT
        r.id,
        r.team_report_id,
        r.public_id,
        r.title,
        r.status,
        r.responsible_user_id,
        r.past_responsible_user_id,
        r.updated_at,
        r.creator_team_id
    FROM
        public.reports AS r
    WHERE
        r.id = _report_id;
END;
$$;

DROP FUNCTION IF EXISTS public.list_reports_internal(int[]);
CREATE OR REPLACE FUNCTION public.list_reports_internal(_report_ids int[])
    RETURNS TABLE(
        id int,
        team_report_id int,
        title text,
        status text,
        created_at timestamp with time zone,
        updated_at timestamp with time zone,
        creator_user_id text,
        creator_team_id text,
        responsible_user_id text,
        past_responsible_user_id text)
    LANGUAGE sql
    STABLE
    AS $$
    SELECT
        r.id,
        r.team_report_id,
        r.title,
        r.status,
        r.created_at,
        r.updated_at,
        r.creator_user_id,
        r.creator_team_id,
        r.responsible_user_id,
        r.past_responsible_user_id
    FROM
        public.reports r
    WHERE
        r.id = ANY(_report_ids)
$$;
